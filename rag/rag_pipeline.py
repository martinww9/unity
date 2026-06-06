# rag_pipeline.py
import os
import json
import re
import random
import pymupdf
from jsonschema import validate, ValidationError

from llama_index.core import (
    VectorStoreIndex, 
    SimpleDirectoryReader, 
    Settings, 
    StorageContext, 
    load_index_from_storage,
    Document
)
from llama_index.core.node_parser import SentenceSplitter
from llama_index.embeddings.ollama import OllamaEmbedding
from llama_index.llms.ollama import Ollama

# Variable interna del módulo para sostener el índice en memoria
_idx = None

# Schemas de respuestas para el llm
SCHEMA_QUESTION = {
    "$schema": "http://json-schema.org/draft-07/schema#",
    "type": "object",
    "required": ["id", "question", "options", "correctAnswerIndex", "dificultad", "puntaje", "explanation"],
    "properties": {
        "id":                 { "type": "string" },
        "question":           { "type": "string" },
        "options":            { "type": "array", "minItems": 4, "maxItems": 4, "items": { "type": "string" } },
        "correctAnswerIndex": { "type": "integer", "minimum": 0, "maximum": 3 },
        "dificultad":         { "type": "string" },
        "puntaje":            { "type": "integer" },
        "explanation":        { "type": "string", "minLength": 1 },
    }
}

PATRONES_INSTITUCIONAL = [
    r'universidad',
    r'facultad',
    r'docente',
    r'profesor[a]?',
    r'asignatura',
    r'carrera',
    r'semestre',
    r'año\s+\d{4}',
    r'\b(Ph\.?D|Dr[as]?|Dres)\b\.?',
    r'\b(Lic|Ing|Mg|Msc|Prof)\b\.?'
]

# Número de páginas iniciales a saltar por PDF (portada, contraportada, índice, etc.)
PAGINAS_PORTADA = 1

def configurar_modelos():
    """Configura el LLM y el modelo de embeddings en los Settings globales de LlamaIndex."""
    print("[1/4] Configurando modelo LLM...")
    llm = Ollama(
        model="a-bot", 
        request_timeout=600.0,
        context_window=5120,
        additional_kwargs={
            "num_ctx": 5120,
            "num_predict": 1024
        }
    )
    print("[2/4] Configurando modelo de Embeddings (nomic-embed-text)...")
    Settings.embed_model = OllamaEmbedding(model_name="nomic-embed-text")
    Settings.llm = llm
    
    Settings.text_splitter = SentenceSplitter(
        chunk_size=512,
        chunk_overlap=50,
        paragraph_separator="\n\n"
    )

def limpiar_texto(texto):
    """Limpia cadenas obvias de metadatos antes del indexado."""
    texto = texto.encode("utf-8", errors="ignore").decode("utf-8")
    reemplazos = {"¡": "á", "£": "á", "©": "é", "³": "ó", "²": "í", "º": "ú"}
    for malo, bueno in reemplazos.items():
        texto = texto.replace(malo, bueno)

    basura = [
        "application/pdf", "PDFlib", "XMP", "DocumentID", "InstanceID", 
        "Acrobat", "metadata", "www.", "http://", "https://"
    ]
    for b in basura:
        texto = texto.replace(b, "")
    texto = re.sub(r'[^\w\s\.,;:¿?\(\)\-áéíóúÁÉÍÓÚñÑüÜ$€%]', ' ', texto)
    texto = re.sub(r'\s+', ' ', texto)
    return texto.strip()

def es_chunk_institucional(texto: str) -> bool:
    """Detecta chunks con datos de portada o encabezados universitarios.
    Requiere 2+ señales para evitar falsos positivos en contenido académico legítimo.
    """
    texto_lower = texto.lower()
    coincidencias = sum(
        1 for p in PATRONES_INSTITUCIONAL
        if re.search(p, texto_lower)
    )
    return coincidencias >= 2

def nodo_util(texto):
    """Heurística bilingüe relajada para proteger acrónimos de informática."""
    if len(texto) < 100:
        return False

    texto_lower = texto.lower()
    conectores = [" de ", " la ", " el ", " que ", " en ", " para ", " the ", " of ", " and ", " to ", " is ", " in "]
    coincidencias = sum(1 for c in conectores if c in texto_lower)
    if coincidencias == 0:
        return False

    if re.search(r'(.)\s\1\s\1', texto_lower):
        return False

    simbolos = sum(1 for c in texto if not c.isalnum() and not c.isspace())
    if simbolos > len(texto) * 0.15:
        return False

    if es_chunk_institucional(texto):
        return False

    return True

def preguntas_son_similares(q1: str, q2: str) -> bool:
    """Detecta preguntas semánticamente equivalentes normalizando el texto."""
    def normalizar(t):
        t = t.lower().strip("¿?")
        t = re.sub(r'\b(qué|cuál|es|el|la|un|una|de|que|se|cuando|como|por)\b', '', t)
        t = re.sub(r'\s+', ' ', t).strip()
        return t

    return normalizar(q1) == normalizar(q2)

def extraer_texto_pdf(data_dir: str) -> list:
    """Extrae texto real de PDFs usando PyMuPDF, saltando páginas de portada."""
    documentos = []
    for root, _, files in os.walk(data_dir):
        for fname in files:
            if not fname.lower().endswith(".pdf"):
                continue
            fpath = os.path.join(root, fname)
            try:
                pdf = pymupdf.open(fpath)
                texto_completo = []
                for i, page in enumerate(pdf):
                    if i < PAGINAS_PORTADA:
                        continue                         # ← salta portada/s
                    texto_pagina = page.get_text("text")
                    if texto_pagina.strip():
                        texto_completo.append(texto_pagina)
                pdf.close()

                if texto_completo:
                    texto_unido = "\n\n".join(texto_completo)
                    documentos.append(
                        Document(
                            text=texto_unido,
                            metadata={"file_name": fname, "file_path": fpath}
                        )
                    )
                    print(f"      [OK] {fname}: {len(texto_unido)} chars extraídos (saltadas {PAGINAS_PORTADA} página/s)")
                else:
                    print(f"      [!] {fname}: PyMuPDF no extrajo texto.")
            except Exception as e:
                print(f"      [ERR] {fname}: {e}")
    return documentos

def cargar_o_crear_index(data_dir="data", storage_dir="storage"):
    """Inicializa, sincroniza y retorna el VectorStoreIndex corporativo."""
    global _idx
    if not os.path.exists(data_dir):
        os.makedirs(data_dir)
        
    necesita_actualizar = True
    docstore_path = os.path.join(storage_dir, "docstore.json")
    
    if os.path.exists(storage_dir) and os.path.exists(docstore_path):
        tiempo_indice = os.path.getmtime(docstore_path)
        necesita_actualizar = False
        
        for root, dirs, files in os.walk(data_dir):
            for file in files:
                filepath = os.path.join(root, file)
                if os.path.getmtime(filepath) > tiempo_indice:
                    necesita_actualizar = True
                    break
            if necesita_actualizar: break

    if not necesita_actualizar:
        print(f"[3/4] Cargando el índice desde '{storage_dir}'...")
        storage_context = StorageContext.from_defaults(persist_dir=storage_dir)
        _idx = load_index_from_storage(storage_context)
        return _idx

    print(f"      -> Procesando PDFs con clonación inmutable...")
    documents = extraer_texto_pdf(data_dir)
    
    if not documents:
        print("      [!] ALERTA: ¡No se encontraron archivos dentro de la carpeta 'data'!")
        _idx = VectorStoreIndex.from_documents([])
        return _idx

    print("\n" + "="*50)
    print("DIAGNÓSTICO: MUESTRA DEL TEXTO EXTRAÍDO DEL PDF:")
    if documents: print(documents[0].get_content()[:400])
    print("="*50 + "\n")

    documentos_limpios = []
    for doc in documents:
        texto_limpio = limpiar_texto(doc.get_content())
        nuevo_doc = Document(text=texto_limpio, metadata=doc.metadata)
        documentos_limpios.append(nuevo_doc)

    if not os.path.exists(storage_dir):
        print(f"[3/4] Creando índice nuevo desde cero...")
        _idx = VectorStoreIndex.from_documents(documentos_limpios, show_progress=True, embed_model=Settings.embed_model)
        _idx.storage_context.persist(persist_dir=storage_dir)
    else:
        print(f"[3/4] Actualizando índice...")
        storage_context = StorageContext.from_defaults(persist_dir=storage_dir)
        _idx = load_index_from_storage(storage_context)
        cambios_realizados = _idx.refresh_ref_docs(documentos_limpios, show_progress=True)
        if any(cambios_realizados):
            _idx.storage_context.persist(persist_dir=storage_dir)
            
    return _idx

def extraer_json(raw_text):
    match = re.search(r'\{.*\}', raw_text, re.DOTALL)
    if not match:
        raise ValueError("No se encontró JSON válido")
    return match.group(0)

def validar_pregunta(q):
    try:
        validate(instance=q, schema=SCHEMA_QUESTION)
        explanation = str(q.get("explanation") or q.get("justificacion") or "").strip()
        if not explanation:
            print("    [SCHEMA] ✗ explanation vacía")
            return False
        return True
    except ValidationError as e:
        print(f"    [SCHEMA] ✗ {e.message}")
        return False

def _temas_fallback() -> list:
    """Temas genéricos de informática usados cuando la inferencia falla."""
    return [
        "tipos de malware ransomware trojan worm", "ataques red phishing spoofing sniffing",
        "cifrado criptografía encryption AES RSA", "firewall IDS IPS detección intrusos",
        "vulnerabilidades CVE exploit buffer overflow", "autenticación MFA contraseñas hash",
        "seguridad redes VPN DMZ protocolos", "ingeniería social manipulación víctima",
        "mitigaciones parches hardening configuración", "normativas ISO 27001 NIST cumplimiento",
        "experiencia del usuario", "interfaces de usuario", "accesibilidad", "heurísticas de Nielsen"
    ]

def inferir_temas_desde_indice(n_temas: int = 10) -> list:
    """Extrae términos representativos del corpus para usar como temas de búsqueda.
    Aplica nodo_util() antes de muestrear para excluir chunks institucionales.
    """
    todos = list(_idx.docstore.docs.values())
    utiles = [n for n in todos if nodo_util(n.text)]  # ← fix 3: filtra antes de muestrear

    if not utiles:
        print("[!] inferir_temas: no hay nodos útiles, usando fallback.")
        return _temas_fallback()

    muestra = random.sample(utiles, min(20, len(utiles)))
    texto_muestra = "\n\n".join([n.text for n in muestra])

    prompt = f"""
Del siguiente texto académico, extrae {n_temas} temas o conceptos técnicos clave.
Devuelve SOLO un JSON con una lista de strings, sin texto adicional.
{{"temas": ["tema 1", "tema 2", ...]}}

TEXTO:
{texto_muestra[:3000]}
"""
    try:
        response = Settings.llm.complete(prompt)
        data = json.loads(extraer_json(response.text))
        temas = data.get("temas", [])
        if temas:
            print(f"[IA] Temas inferidos del corpus: {temas}")
            return temas
    except Exception as e:
        print(f"[!] inferir_temas: error al inferir ({e}), usando fallback.")

    return _temas_fallback()

def pasar_por_critico(pregunta: dict, contexto: str) -> dict:
    """Valida y ajusta la pregunta usando el agente crítico del LLM."""
    CRITERIOS_CRITICO = """
Eres un evaluador estricto de preguntas de trivia académica. Analiza la pregunta y responde SOLO con JSON válido.
CRITERIOS DE RECHAZO (si alguno falla, reformula):
1. Si la pregunta menciona "el contexto proporcionado" o "el texto", se debe reformular
2. Si la respuesta correcta es ambigua o hay 2 opciones correctas, se debe reformular
3. Hay palabras en otro idioma, se debe corregir al español
4. El concepto central no existe realmente, crear otra pregunta
5. Las opciones incorrectas son obviamente absurdas, crear otro conjunto de respuestas

FORMATO DE RESPUESTA:
{
    "aprobada": true | false,
    "motivo": "explicación",
    "pregunta_final": {
        "id": "string",
        "question": "¿Pregunta?",
        "options": ["string", "string", "string", "string"],
        "correctAnswerIndex": 0,
        "dificultad": "string",
        "puntaje": 10,
        "explanation": "1-2 frases que justifiquen la alternativa correcta"
    }
}
"""
    explanation_original = str(pregunta.get("explanation", "")).strip()
    prompt_critico = f"{CRITERIOS_CRITICO}\n\nCONTEXTO:\n{contexto}\n\nPREGUNTA:\n{json.dumps(pregunta, ensure_ascii=False)}\n\nResponde SOLO con el JSON."

    for intento in range(2):
        try:
            response = Settings.llm.complete(prompt_critico)
            resultado = json.loads(extraer_json(response.text))
            pregunta_final = resultado.get("pregunta_final", pregunta)

            pregunta_final["id"] = pregunta["id"]
            pregunta_final["dificultad"] = pregunta["dificultad"]
            pregunta_final["puntaje"] = pregunta["puntaje"]

            texto = pregunta_final.get("question", "").strip()
            if not texto.startswith("¿"): texto = "¿" + texto
            if not texto.endswith("?"): texto = texto + "?"
            pregunta_final["question"] = texto

            if not str(pregunta_final.get("explanation", "")).strip():
                pregunta_final["explanation"] = explanation_original or str(
                    pregunta.get("explanation") or pregunta.get("justificacion") or ""
                ).strip()

            if not validar_pregunta(pregunta_final):
                print(f"    [CRÍTICO] Estructura inválida tras revisión, usando original")
                return pregunta

            return pregunta_final
        except Exception as e:
            print(f"    [CRÍTICO] Error intento {intento+1}: {e}")
    return pregunta

def ejecutar_pipeline_preguntas(ia_state, state_lock):
    """Tarea iterativa para generar las preguntas. Recibe el estado para actualizarlo de forma segura."""
    global _idx
    if _idx is None:
        print("[!] Error: No se puede generar preguntas sin un índice inicializado.")
        return

    conceptos_usados = set()

    print("[IA] Generando preguntas...")
    dificultades = [
        ("Fácil", 10), ("Fácil", 10), ("Fácil", 10), ("Fácil", 10),
        ("Media", 20), ("Media", 20), ("Media", 20),
        ("Difícil", 30), ("Difícil", 30), ("Difícil", 30)
    ]

    temas_busqueda = inferir_temas_desde_indice(n_temas=10)
    preguntas_generadas = []

    total_objetivo = len(dificultades)
    guardadas = 0
    intentos_globales = 0
    MAX_INTENTOS_GLOBALES = total_objetivo * 3  # techo de seguridad: máximo 30 ciclos

    while guardadas < total_objetivo and intentos_globales < MAX_INTENTOS_GLOBALES:
        dificultad, puntaje = dificultades[guardadas]
        intentos_globales += 1

        tema_actual = temas_busqueda[intentos_globales % len(temas_busqueda)]
        nodos_brutos = _idx.as_retriever(similarity_top_k=6).retrieve(tema_actual)
        nodos_filtrados = [n for n in nodos_brutos if nodo_util(n.text)]

        if not nodos_filtrados:
            todos_los_nodos = list(_idx.docstore.docs.values())
            nodos_filtrados = [n for n in todos_los_nodos if nodo_util(n.text)]
            if nodos_filtrados:
                nodos_filtrados = random.sample(nodos_filtrados, min(3, len(nodos_filtrados)))
            else:
                nodos_filtrados = nodos_brutos

        nodos_utiles = [n for n in nodos_filtrados if nodo_util(n.text)]
        contexto = "\n\n".join([n.text for n in nodos_utiles[:3]])

        prompt = f"""
CONTEXTO DE ESTUDIO:
{contexto}

INSTRUCCIONES:
- Genera UNA pregunta técnica basada SOLO en conceptos explícitamente mencionados en el contexto.
- Si el contexto no contiene información técnica útil, genera una pregunta sobre: Ingeniería Informática.
- PROHIBIDO usar la frase "el contexto proporcionado" en el texto de la pregunta.
- Nivel: {dificultad} | Puntaje: {puntaje}
- Incluye "explanation": 1 o 2 frases en español que justifiquen por qué la alternativa correcta es la adecuada.

JSON REQUERIDO:
{{
    "id": "{guardadas + 1}_{dificultad}",
    "question": "¿Pregunta técnica concreta?",
    "options": ["Opción A", "Opción B", "Opción C", "Opción D"],
    "correctAnswerIndex": 0,
    "dificultad": "{dificultad}",
    "puntaje": {puntaje},
    "explanation": "Breve justificación de por qué la alternativa correcta es la adecuada."
}}
"""
        pregunta_valida = None
        for intento in range(3):
            try:
                response = Settings.llm.complete(prompt)
                data = json.loads(extraer_json(response.text))
                if validar_pregunta(data):
                    pregunta_valida = data
                    break
            except:
                pass

        if not pregunta_valida:
            print(f"[!] Ciclo {intentos_globales}: no se obtuvo pregunta válida, reintentando...")
            continue

        contexto_usado = "\n\n".join([n.text for n in nodos_filtrados[:3]])
        pregunta_revisada = pasar_por_critico(pregunta_valida, contexto_usado)

        if any(preguntas_son_similares(pregunta_revisada["question"], q) for q in conceptos_usados):
            print(f"[~] Ciclo {intentos_globales}: duplicada semántica, buscando variante...")
            continue

        # Pregunta única y válida: guardar
        conceptos_usados.add(pregunta_revisada["question"])
        pregunta_revisada["id"] = f"{guardadas + 1}_{dificultad}"
        preguntas_generadas.append(pregunta_revisada)
        with state_lock:
            ia_state["questions"].append(pregunta_revisada)
        guardadas += 1
        print(f"[✓] {guardadas}/{total_objetivo}: {pregunta_revisada['question'][:45]}...")

    if guardadas < total_objetivo:
        print(f"[!] Techo alcanzado: se generaron {guardadas}/{total_objetivo} preguntas.")

    with state_lock:
        ia_state["status"] = "completed"

    with open("preguntas_generadas.json", "w", encoding="utf-8") as f:
        json.dump({"questions": preguntas_generadas}, f, ensure_ascii=False, indent=4)
