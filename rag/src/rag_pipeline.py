# rag_pipeline.py
import os
import json
import re
import random
import pymupdf
import sys, pathlib
from jsonschema import validate, ValidationError

from llama_index.core import (
    VectorStoreIndex, 
    SimpleDirectoryReader, 
    Settings, 
    StorageContext, 
    load_index_from_storage,
    Document
)
from llama_index.core.node_parser import MarkdownNodeParser, SentenceSplitter
from llama_index.embeddings.ollama import OllamaEmbedding
from llama_index.llms.ollama import Ollama

# Variable interna del modulo para sostener el indice en memoria
_idx = None

TEMAS_POR_NIVEL = {
    1: [
        "tipo dato primitivo",
        "tipo dato abstracto TDA",
        "lista enlazada nodo puntero",
        "lista doblemente enlazada variantes",
        "pila LIFO push pop",
        "cola FIFO enqueue dequeue",
        "arreglo vector memoria contigua",
        "mapa no ordenado implementacion",
        "patron de diseno iterador acceso secuencial",
        "insercion eliminacion busqueda estructuras lineales"
    ],
    2: [
        "complejidad algoritmica notacion O grande Big O",
        "complejidad temporal iterativa recursiva",
        "complejidad espacial estructuras lineales no lineales",
        "cola con prioridad monticulo heap min max",
        "arbol binario busqueda BST recorrido inorden",
        "arbol AVL balanceo rotacion izquierda derecha",
        "arbol rojo negro propiedades insercion",
        "arbol B arbol B plus paginacion nodo",
        "skip list estructura probabilistica",
        "trie arbol prefijo cadena"
    ],
    3: [
        "grafo vertice arista dirigido ponderado",
        "representacion grafo lista adyacencia matriz",
        "recorrido profundidad DFS anchura BFS",
        "algoritmo Dijkstra camino minimo",
        "algoritmo busqueda A estrella heuristica",
        "tabla hash funcion hashing dispersion",
        "resolucion colisiones encadenamiento sondeo lineal",
        "mapa conjunto ordenado",
        "implementacion iterador arbol busqueda",
        "funcion ad hoc is_equal lower_than"
    ]
}

# Configuración balanceada a 6 preguntas por nivel
DIFICULTADES_POR_NIVEL = {
    1: [("Fácil", 10)] * 3 + [("Media", 20)] * 3,
    2: [("Fácil", 10)] * 1 + [("Media", 20)] * 4 + [("Difícil", 30)] * 1,
    3: [("Media", 20)] * 1 + [("Difícil", 30)] * 5,
}

# Schemas de respuestas para el llm
SCHEMA_QUESTION = {
    "$schema": "http://json-schema.org/draft-07/schema#",
    "type": "object",
    "required": ["id", "question", "options", "correctAnswerIndex", "dificultad", "puntaje"],
    "properties": {
        "id":                 { "type": "string" },
        "question":           { "type": "string" },
        "options":            { "type": "array", "minItems": 4, "maxItems": 4, "items": { "type": "string" } },
        "correctAnswerIndex": { "type": "integer", "minimum": 0, "maximum": 3 },
        "dificultad":         { "type": "string" },
        "puntaje":            { "type": "integer" }
    }
}

SCHEMA_FEEDBACK = {
    "$schema": "http://json-schema.org/draft-07/schema#",
    "type": "object",
    "required": ["mensaje_general", "fortalezas", "areas_mejora"],
    "properties": {
        "mensaje_general":      { "type": "string" },
        "fortalezas":           { "type": "array", "items": { "type": "string" } },
        "areas_mejora":         { "type": "array", "items": { "type": "string" } },
    }
}

PATRONES_INSTITUCIONAL = [
    r'universidad', r'facultad', r'docente', r'profesor[a]?', r'asignatura',
    r'carrera', r'semestre', r'año\s+\d{4}', r'\b(Ph\.?D|Dr[as]?|Dres)\b\.?', r'\b(Lic|Ing|Mg|Msc|Prof)\b\.?'
]

PAGINAS_PORTADA = 1

PROJECT_ROOT = pathlib.Path(__file__).resolve().parent.parent
DATA_DIR = PROJECT_ROOT / "data"
STORAGE_DIR = PROJECT_ROOT / "storage"
OUTPUT_DIR = PROJECT_ROOT / "output"

os.makedirs(STORAGE_DIR, exist_ok=True)
os.makedirs(OUTPUT_DIR, exist_ok=True)

def configurar_modelos():
    print("[1/4] Configurando modelo LLM...")
    llm = Ollama(
        model="llama3.2:latest",
        request_timeout=600.0,
        context_window=5120,
        additional_kwargs={"num_ctx": 5120, "num_predict": 1024}
    )
    print("[2/4] Configurando modelo de Embeddings (nomic-embed-text)...")
    Settings.embed_model = OllamaEmbedding(model_name="nomic-embed-text")
    Settings.llm = llm
    
    # Reducimos el chunk_size a 400 y lo establecemos como parser primario global.
    # Esto elimina el error 400 al garantizar que ningún nodo sature la ventana de contexto de nomic.
    Settings.text_splitter = SentenceSplitter(
        chunk_size=400, 
        chunk_overlap=60, 
        paragraph_separator="\n\n"
    )
    
def limpiar_texto(texto):
    texto = texto.encode("utf-8", errors="ignore").decode("utf-8")
    reemplazos = {"¡": "á", "£": "á", "©": "é", "³": "ó", "²": "í", "º": "ú"}
    for malo, bueno in reemplazos.items():
        texto = texto.replace(malo, bueno)
    basura = ["application/pdf", "PDFlib", "XMP", "DocumentID", "InstanceID", "Acrobat", "metadata", "www.", "http://", "https://"]
    for b in basura:
        texto = texto.replace(b, "")
    # Conservamos símbolos matemáticos fundamentales ($ y _) para proteger la notación asintótica y ecuaciones LaTeX
    texto = re.sub(r'[^\w\s\.,;:¿?\(\)\-áéíóúÁÉÍÓÚñÑüÜ$€%_<=#\+\*\/\{\}\[\]\^]', ' ', texto)
    texto = re.sub(r'\s+', ' ', texto)
    return texto.strip()

def es_chunk_institucional(texto: str) -> bool:
    texto_lower = texto.lower()
    coincidencias = sum(1 for p in PATRONES_INSTITUCIONAL if re.search(p, texto_lower))
    return coincidencias >= 2

def nodo_util(texto):
    if len(texto) < 60:  # Umbral ligeramente menor para no descartar bloques puros de código o ecuaciones legítimas
        return False
    texto_lower = texto.lower()
    conectores = [" de ", " la ", " el ", " que ", " en ", " para ", " the ", " of ", " and ", " to ", " is ", " in "]
    if sum(1 for c in conectores if c in texto_lower) == 0 and not ("for" in texto_lower or "while" in texto_lower or "O(" in texto):
        return False
    if re.search(r'(.)\s\1\s\1', texto_lower):
        return False
    if es_chunk_institucional(texto):
        return False
    return True

def preguntas_son_similares(q1: str, q2: str) -> bool:
    def normalizar(t):
        t = t.lower().strip("¿?")
        t = re.sub(r'\b(qué|cuál|es|el|la|un|una|de|que|se|cuando|como|por)\b', '', t)
        t = re.sub(r'\s+', ' ', t).strip()
        return t
    return normalizar(q1) == normalizar(q2)

def extraer_texto_pdf(data_dir: str) -> list:
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
                        continue
                    # Se mantiene la extracción de texto estándar pero preservando la codificación posicional para ecuaciones
                    texto_pagina = page.get_text("text")
                    if texto_pagina.strip():
                        texto_completo.append(texto_pagina)
                pdf.close()
                if texto_completo:
                    texto_unido = "\n\n".join(texto_completo)
                    documentos.append(Document(
                        text=texto_unido,
                        metadata={"file_name": fname, "file_path": fpath}
                    ))
                    print(f"      [OK] {fname}: {len(texto_unido)} chars extraídos")
                else:
                    print(f"      [!] {fname}: sin texto extraíble.")
            except Exception as e:
                print(f"      [ERR] {fname}: {e}")
    return documentos

def cargar_o_crear_index(data_dir=None, storage_dir=None):
    global _idx
    data_dir = str(data_dir or DATA_DIR)
    storage_dir = str(storage_dir or STORAGE_DIR)

    if not os.path.exists(data_dir):
        os.makedirs(data_dir)

    necesita_actualizar = True
    docstore_path = os.path.join(storage_dir, "docstore.json")

    if os.path.exists(storage_dir) and os.path.exists(docstore_path):
        tiempo_indice = os.path.getmtime(docstore_path)
        necesita_actualizar = False
        for root, dirs, files in os.walk(data_dir):
            for file in files:
                if os.path.getmtime(os.path.join(root, file)) > tiempo_indice:
                    necesita_actualizar = True
                    break
            if necesita_actualizar:
                break

    if not necesita_actualizar:
        print(f"[3/4] Cargando índice desde '{storage_dir}'...")
        _idx = load_index_from_storage(StorageContext.from_defaults(persist_dir=storage_dir))
        return _idx

    print("      -> Procesando PDFs...")
    documents = extraer_texto_pdf(data_dir)

    if not documents:
        print("      [!] No se encontraron PDFs en 'data'.")
        _idx = VectorStoreIndex.from_documents([])
        return _idx

    documentos_limpios = [
        Document(text=limpiar_texto(doc.get_content()), metadata=doc.metadata)
        for doc in documents
    ]

    indice_valido = os.path.exists(docstore_path)

    if not indice_valido:
        print("[3/4] Creando índice nuevo...")
        _idx = VectorStoreIndex.from_documents(
            documentos_limpios, show_progress=True, embed_model=Settings.embed_model
        )
        _idx.storage_context.persist(persist_dir=storage_dir)
    else:
        print("[3/4] Actualizando índice...")
        storage_context = StorageContext.from_defaults(persist_dir=storage_dir)
        _idx = load_index_from_storage(storage_context)
        cambios = _idx.refresh_ref_docs(documentos_limpios, show_progress=True)
        if any(cambios):
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
        return True
    except ValidationError as e:
        print(f"    [SCHEMA] ✗ {e.message}")
        return False

def _temas_fallback(nivel: int) -> list:
    return TEMAS_POR_NIVEL.get(nivel, TEMAS_POR_NIVEL[1])

def inferir_temas_desde_indice(nivel: int, n_temas: int = 10) -> list:
    todos = list(_idx.docstore.docs.values())
    utiles = [n for n in todos if nodo_util(n.text)]

    if not utiles:
        print(f"[!] inferir_temas nivel {nivel}: sin nodos útiles, usando fallback.")
        return _temas_fallback(nivel)

    muestra = random.sample(utiles, min(20, len(utiles)))
    texto_muestra = "\n\n".join([n.text for n in muestra])
    temas_guia = ", ".join(TEMAS_POR_NIVEL[nivel][:5])

    prompt = f"""
Del siguiente texto académico, extrae {n_temas} temas o conceptos técnicos clave
relacionados con: {temas_guia}.
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
            print(f"[IA] Temas inferidos (nivel {nivel}): {temas}")
            return temas
    except Exception as e:
        print(f"[!] inferir_temas nivel {nivel}: error ({e}), usando fallback.")

    return _temas_fallback(nivel)

def pasar_por_critico(pregunta: dict, contexto: str) -> dict:
    CRITERIOS = """
Eres un evaluador estricto de preguntas de trivia académica. Responde SOLO con JSON válido.
CRITERIOS DE RECHAZO (si alguno falla, reformula):
1. La pregunta menciona "el contexto proporcionado" o "el texto" → reformular
2. La respuesta correcta es ambiguas o hay 2 opciones correctas → reformular
3. Hay palabras en otro idioma → corregir al español
4. Si hay ecuaciones, fórmulas o expresiones asintóticas (Big O), su sintaxis matemática o pseudocódigo debe ser exacta → corregir
5. Las opciones incorrectas son obviamente absurdas → mejorarlas

FORMATO DE RESPUESTA:
{
    "aprobada": true | false,
    "motivo": "explicación",
    "pregunta_final": { ...mismo JSON... }
}
"""
    contexto_seguro = contexto.encode("utf-8", errors="ignore").decode("utf-8")
    contexto_seguro = re.sub(r'\\u[0-9a-fA-F]{0,3}(?![0-9a-fA-F])', '', contexto_seguro)
    prompt = f"{CRITERIOS}\n\nCONTEXTO:\n{contexto_seguro}\n\nPREGUNTA:\n{json.dumps(pregunta, ensure_ascii=False)}\n\nResponde SOLO con el JSON."

    for intento in range(2):
        try:
            resultado = json.loads(extraer_json(Settings.llm.complete(prompt).text))
            pregunta_final = resultado.get("pregunta_final", pregunta)
            pregunta_final["id"] = pregunta["id"]
            pregunta_final["dificultad"] = pregunta["dificultad"]
            pregunta_final["puntaje"] = pregunta["puntaje"]

            texto = pregunta_final.get("question", "").strip()
            if not texto.startswith("¿"): texto = "¿" + texto
            if not texto.endswith("?"): texto = texto + "?"
            pregunta_final["question"] = texto

            if not validar_pregunta(pregunta_final):
                print("    [CRÍTICO] Estructura inválida tras revisión, usando original")
                return pregunta
            return pregunta_final
        except Exception as e:
            print(f"    [CRÍTICO] Error intento {intento+1}: {e}")
    return pregunta

def necesita_critico(q: dict) -> bool:
    texto = q.get("question", "")
    opciones = q.get("options", [])
    return any([
        "contexto" in texto.lower(),
        "el texto" in texto.lower(),
        not texto.strip().startswith("¿"),
        any(len(op.split()) <= 1 for op in opciones),
        len(set(op.lower() for op in opciones)) < 4,
    ])

def ejecutar_pipeline_nivel(nivel: int, ia_state: dict, state_lock) -> list:
    global _idx
    if _idx is None:
        print(f"[!] Error: índice no inicializado (nivel {nivel}).")
        return []

    print(f"\n[IA] Generando preguntas — Nivel {nivel}...")
    dificultades = DIFICULTADES_POR_NIVEL[nivel]
    temas_busqueda = inferir_temas_desde_indice(nivel, n_temas=10)

    preguntas_generadas = []
    conceptos_usados = set()

    total_objetivo = len(dificultades)
    guardadas = 0
    intentos_globales = 0
    MAX_INTENTOS = total_objetivo * 3

    while guardadas < total_objetivo and intentos_globales < MAX_INTENTOS:
        dificultad, puntaje = dificultades[guardadas]
        intentos_globales += 1

        tema_actual = temas_busqueda[intentos_globales % len(temas_busqueda)]
        # Aproximamos Parent Retrieval incrementando similarity_top_k para consolidar fragmentos adyacentes de la misma sección
        nodos_brutos = _idx.as_retriever(similarity_top_k=8).retrieve(tema_actual)
        nodos_filtrados = [n for n in nodos_brutos if nodo_util(n.text)]

        if not nodos_filtrados:
            todos = list(_idx.docstore.docs.values())
            nodos_filtrados = [n for n in todos if nodo_util(n.text)]
            if nodos_filtrados:
                nodos_filtrados = random.sample(nodos_filtrados, min(4, len(nodos_filtrados)))
            else:
                nodos_filtrados = nodos_brutos

        # Agrupamos un contexto más amplio para asegurar coherencia en explicaciones de código o fórmulas matemáticas
        contexto = "\n\n".join([n.text for n in nodos_filtrados[:4]])

        # ✓ ADAPTACIÓN PEDAGÓGICA: Incorporamos una condición Low Guidance (Prompt 1 del estudio)
        # Esto incrementa la alineación curricular sin penalizar la flexibilidad adaptativa del LLM
        prompt = f"""
CONTEXTO DE ESTUDIO (Estructuras de Datos y Algoritmos — Nivel {nivel}):
{contexto}

INSTRUCCIONES DE DISEÑO:
- Genera UNA pregunta técnica de opción múltiple basada en el contexto anterior.
- Tema específico objetivo: {tema_actual}
- Nivel de dificultad cognitiva requerido: {dificultad} | Puntaje asignado: {puntaje}
- Use ejemplos y lenguaje de la sección del contexto provisto para dar formato a su respuesta.
- Si el contexto incluye fórmulas matemáticas, notación Big-O (ej: O(n log n)) o bloques de código, mantenga su sintaxis matemática intacta tanto en la pregunta como en las opciones.
- PROHIBIDO usar la frase "el contexto proporcionado", "según el texto" o referencias explícitas al documento de lectura.

JSON REQUERIDO:
{{
    "id": "N{nivel}_{guardadas + 1}_{dificultad}",
    "question": "¿Pregunta técnica concreta enfocada en el concepto?",
    "options": ["Opción A (Exacta)", "Opción B", "Opción C", "Opción D"],
    "correctAnswerIndex": 0,
    "dificultad": "{dificultad}",
    "puntaje": {puntaje}
}}
"""
        pregunta_valida = None
        for intento in range(3):
            try:
                data = json.loads(extraer_json(Settings.llm.complete(prompt).text))
                if validar_pregunta(data):
                    pregunta_valida = data
                    break
            except:
                pass

        if not pregunta_valida:
            continue

        contexto_usado = "\n\n".join([n.text for n in nodos_filtrados[:4]])

        if necesita_critico(pregunta_valida):
            pregunta_revisada = pasar_por_critico(pregunta_valida, contexto_usado)
        else:
            pregunta_revisada = pregunta_valida

        if any(preguntas_son_similares(pregunta_revisada["question"], q) for q in conceptos_usados):
            print(f"[~] Nivel {nivel} — Ciclo {intentos_globales}: duplicada semántica, buscando otra variante...")
            continue

        conceptos_usados.add(pregunta_revisada["question"])
        pregunta_revisada["id"] = f"N{nivel}_{guardadas + 1}_{dificultad}"
        preguntas_generadas.append(pregunta_revisada)

        with state_lock:
            ia_state["levels"][nivel]["questions"].append(pregunta_revisada)

        guardadas += 1
        print(f"[✓] Nivel {nivel} — {guardadas}/{total_objetivo}: {pregunta_revisada['question'][:50]}...")

    if guardadas < total_objetivo:
        print(f"[!] Nivel {nivel}: límite alcanzado, generadas {guardadas}/{total_objetivo}.")

    return preguntas_generadas

def ejecutar_pipeline_todos_los_niveles(ia_state: dict, state_lock):
    for nivel in [1, 2, 3]:
        with state_lock:
            ia_state["levels"][nivel]["status"] = "generating"

        preguntas = ejecutar_pipeline_nivel(nivel, ia_state, state_lock)

        with state_lock:
            ia_state["levels"][nivel]["status"] = "completed" if preguntas else "error"

        fname = OUTPUT_DIR / f"preguntas_nivel_{nivel}.json"
        with open(fname, "w", encoding="utf-8") as f:
            json.dump({"nivel": nivel, "questions": preguntas}, f, ensure_ascii=False, indent=4)
        print(f"[✓] Nivel {nivel} guardado en {fname}")

    with state_lock:
        ia_state["status"] = "completed"

def ejecutar_pipeline_feedback(puntaje: int, total: int, nivel: int = 3):
    global _idx
    if _idx is None:
        raise RuntimeError("Índice no inicializado.")

    retriever = _idx.as_retriever(similarity_top_k=4)
    temas_nivel = " ".join(TEMAS_POR_NIVEL.get(nivel, TEMAS_POR_NIVEL[3])[:3])
    nodos = retriever.retrieve(temas_nivel)
    contexto = "\n\n".join([n.text for n in nodos if nodo_util(n.text)])

    # Condición Low-Guidance aplicada también a la generación de la retroalimentación final
    prompt = f"""
CONTEXTO ACADÉMICO (Estructuras de Datos):
{contexto}

TAREA:
Un estudiante obtuvo {puntaje} de {total} puntos totales en los 3 niveles de una trivia sobre estructuras de datos.
Genera una retroalimentación formativa en español y JSON estricto.
Use ejemplos y el lenguaje de la sección del contexto académico provisto para estructurar la respuesta.

JSON REQUERIDO:
{{
    "mensaje_general": "Resumen breve del desempeño formativo",
    "fortalezas": ["Concepto 1 verificado", "Concepto 2 verificado"],
    "areas_mejora": ["Área 1 por reforzar", "Área 2 por reforzar"]
}}
"""
    for intento in range(3):
        try:
            response = Settings.llm.complete(prompt)
            json_str = extraer_json(response.text)
            data = json.loads(json_str)
            
            validate(instance=data, schema=SCHEMA_FEEDBACK)
            print(f"      [OK] Feedback generado correctamente (Intento {intento+1}).")
            return data
        except Exception as e:
            print(f"      [!] Error de formato en feedback (Intento {intento+1}/3): {e}")
            
    print("      [🚨] Fallaron los 3 intentos. Enviando feedback de respaldo.")
    return {
        "mensaje_general": f"Obtuviste {puntaje} de {total} puntos. Tu reporte detallado no pudo ser procesado en este momento.",
        "fortalezas": ["Completaste la trivia satisfactoriamente."],
        "areas_mejora": ["Revisa el material de estudio nuevamente para reforzar conceptos."]
    }