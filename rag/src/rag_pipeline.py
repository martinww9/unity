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
    r'universidad', r'facultad', r'docente', r'profesor[a]?', r'asignatura',
    r'carrera', r'semestre', r'año\s+\d{4}', r'\b(Ph\.?D|Dr[as]?|Dres)\b\.?', r'\b(Lic|Ing|Mg|Msc|Prof)\b\.?'
]

PAGINAS_PORTADA = 1

PROJECT_ROOT = pathlib.Path(__file__).resolve().parent.parent
DATA_DIR = PROJECT_ROOT / "data"
STORAGE_DIR = PROJECT_ROOT / "storage"
OUTPUT_DIR = PROJECT_ROOT / "output"
SCHEMA_VERSION = 1
CANONICAL_GENERATED_FILE = OUTPUT_DIR / "preguntas_generadas.json"
CANONICAL_STATIC_FILE = OUTPUT_DIR / "preguntas_estaticas.json"

os.makedirs(STORAGE_DIR, exist_ok=True)
os.makedirs(OUTPUT_DIR, exist_ok=True)


def cargar_cache_preguntas_en_estado(ia_state: dict) -> bool:
    """Carga preguntas_nivel_*.json en ia_state. True si los 3 niveles tienen preguntas válidas."""
    for canonical_path in (CANONICAL_GENERATED_FILE, CANONICAL_STATIC_FILE):
        if not canonical_path.is_file():
            continue
        try:
            with open(canonical_path, encoding="utf-8") as f:
                payload = json.load(f)
        except (OSError, json.JSONDecodeError) as e:
            print(f"[!] No se pudo leer caché canónica {canonical_path}: {e}")
            continue
        if apply_questions_payload_to_state(payload, ia_state):
            print(f"[RAG] Caché canónica restaurada desde {canonical_path}.")
            return True
        print(f"[RAG] Caché canónica inválida o incompleta: {canonical_path}")

    loaded = 0
    for nivel in [1, 2, 3]:
        fname = OUTPUT_DIR / f"preguntas_nivel_{nivel}.json"
        if not fname.is_file():
            continue
        try:
            with open(fname, encoding="utf-8") as f:
                data = json.load(f)
        except (OSError, json.JSONDecodeError) as e:
            print(f"[!] No se pudo leer caché nivel {nivel}: {e}")
            continue
        questions = data.get("questions") or []
        normalized = normalizar_preguntas_nivel(nivel, questions)
        if len(normalized) != _expected_count(nivel):
            print(f"[RAG] Caché nivel {nivel} ignorada: {len(normalized)}/{_expected_count(nivel)} preguntas válidas.")
            continue
        ia_state["levels"][nivel]["questions"] = normalized
        ia_state["levels"][nivel]["status"] = "completed"
        loaded += 1
    return loaded == 3


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

def validar_pregunta(q, log_errors=True):
    try:
        validate(instance=q, schema=SCHEMA_QUESTION)
        explanation = str(q.get("explanation", "")).strip()
        if not explanation or _es_placeholder(explanation):
            if log_errors:
                print("    [SCHEMA] ✗ explanation vacía o placeholder")
            return False
        return True
    except ValidationError as e:
        if log_errors:
            print(f"    [SCHEMA] ✗ {e.message}")
        return False

def _expected_count(nivel: int) -> int:
    return len(DIFICULTADES_POR_NIVEL[nivel])

def normalizar_preguntas_nivel(nivel: int, questions: list) -> list:
    normalized = []
    if not isinstance(questions, list):
        return normalized

    for idx, question in enumerate(questions[:_expected_count(nivel)]):
        dificultad, puntaje = DIFICULTADES_POR_NIVEL[nivel][idx]
        q = normalizar_pregunta(question, nivel, idx + 1, dificultad, puntaje)
        if q is None or not validar_pregunta(q, log_errors=False):
            continue
        normalized.append(q)
    return normalized

def build_questions_payload(levels_state: dict) -> dict:
    levels = []
    all_completed = True

    for nivel in [1, 2, 3]:
        level_state = levels_state.get(nivel, {})
        questions = normalizar_preguntas_nivel(nivel, level_state.get("questions", []))
        completed = len(questions) == _expected_count(nivel)
        if not completed:
            all_completed = False

        levels.append({
            "nivel": nivel,
            "status": "completed" if completed else level_state.get("status", "idle"),
            "questions": questions,
        })

    return {
        "schemaVersion": SCHEMA_VERSION,
        "status": "completed" if all_completed else "error",
        "levels": levels,
    }

def apply_questions_payload_to_state(payload: dict, ia_state: dict) -> bool:
    if not isinstance(payload, dict) or payload.get("schemaVersion") != SCHEMA_VERSION:
        return False

    levels = payload.get("levels")
    if not isinstance(levels, list):
        return False

    loaded = 0
    by_level = {}
    for level_payload in levels:
        if not isinstance(level_payload, dict):
            continue
        nivel = level_payload.get("nivel")
        if nivel not in [1, 2, 3]:
            continue
        questions = normalizar_preguntas_nivel(nivel, level_payload.get("questions", []))
        if len(questions) != _expected_count(nivel):
            continue
        by_level[nivel] = questions
        loaded += 1

    if loaded != 3:
        return False

    for nivel, questions in by_level.items():
        ia_state["levels"][nivel]["questions"] = questions
        ia_state["levels"][nivel]["status"] = "completed"
    ia_state["status"] = "completed"
    return True

def save_questions_file(levels_state: dict, path=CANONICAL_GENERATED_FILE) -> bool:
    payload = build_questions_payload(levels_state)
    if payload["status"] != "completed":
        print("[RAG] No se guarda contrato canónico: niveles incompletos.")
        return False

    with open(path, "w", encoding="utf-8") as f:
        json.dump(payload, f, ensure_ascii=False, indent=4)
    print(f"[✓] Preguntas canónicas guardadas en {path}")
    return True

def _primer_string_desde_dict(value: dict) -> str:
    for key in ("text", "label", "option", "value", "content"):
        text = value.get(key)
        if isinstance(text, str) and text.strip():
            return text.strip()
    for text in value.values():
        if isinstance(text, str) and text.strip():
            return text.strip()
    return ""

def _normalizar_opcion(value) -> str:
    if isinstance(value, str):
        return value.strip()
    if isinstance(value, dict):
        return _primer_string_desde_dict(value)
    return ""

def _es_placeholder(texto: str) -> bool:
    normalizado = texto.lower().strip(" ¿?().:-")
    placeholders = {
        "pregunta tecnica concreta enfocada en el concepto",
        "pregunta técnica concreta enfocada en el concepto",
        "pregunta tecnica concreta basada en el pdf",
        "pregunta técnica concreta basada en el pdf",
        "opcion a",
        "opción a",
        "opcion b",
        "opción b",
        "opcion c",
        "opción c",
        "opcion d",
        "opción d",
        "opcion a exacta",
        "opción a exacta",
        "exacta",
    }
    return normalizado in placeholders

def normalizar_pregunta(q: dict, nivel: int, index: int, dificultad: str, puntaje: int) -> dict | None:
    if not isinstance(q, dict):
        return None

    raw_options = q.get("options")
    if not isinstance(raw_options, list):
        return None

    options = [_normalizar_opcion(option) for option in raw_options]
    options = [option for option in options if option]
    if len(options) < 4:
        return None
    options = options[:4]

    question = str(q.get("question", "")).strip()
    if not question:
        return None
    if not question.startswith("¿"):
        question = "¿" + question
    if not question.endswith("?"):
        question = question + "?"

    try:
        correct = int(q.get("correctAnswerIndex", 0))
    except (TypeError, ValueError):
        return None
    if correct < 0 or correct > 3:
        return None

    if _es_placeholder(question):
        return None
    if any(_es_placeholder(option) for option in options):
        return None
    if len(set(option.lower() for option in options)) < 4:
        return None

    explanation = str(q.get("explanation") or q.get("justificacion") or "").strip()
    if not explanation or _es_placeholder(explanation):
        return None

    return {
        "id": f"N{nivel}_{index}_{dificultad}",
        "question": question,
        "options": options,
        "correctAnswerIndex": correct,
        "dificultad": str(dificultad),
        "puntaje": int(puntaje),
        "explanation": explanation,
    }

STOPWORDS_TECNICAS = {
    "que", "qué", "cual", "cuál", "como", "cómo", "cuando", "cuándo", "donde", "dónde",
    "para", "por", "con", "sin", "una", "uno", "unos", "unas", "del", "los", "las",
    "este", "esta", "estos", "estas", "sobre", "entre", "segun", "según", "opcion", "opción",
    "respuesta", "correcta", "incorrecta", "pregunta", "concepto", "tecnica", "técnica",
}

def _tokens_significativos(texto: str) -> set[str]:
    tokens = re.findall(r"[A-Za-zÁÉÍÓÚáéíóúÑñÜü_][\wÁÉÍÓÚáéíóúÑñÜü_+-]*", texto.lower())
    return {token for token in tokens if len(token) >= 4 and token not in STOPWORDS_TECNICAS}

def validar_grounding(q: dict, contexto: str, tema_actual: str) -> bool:
    pregunta = q.get("question", "")
    opciones = " ".join(q.get("options", []))
    texto_pregunta = f"{pregunta} {opciones}".lower()
    if any(frase in texto_pregunta for frase in ("contexto", "texto", "documento", "lectura")):
        return False

    tokens_pregunta = _tokens_significativos(texto_pregunta)
    tokens_base = _tokens_significativos(f"{contexto} {tema_actual}")
    if not tokens_pregunta or not tokens_base:
        return False

    return len(tokens_pregunta & tokens_base) > 0

def _temas_fallback(nivel: int) -> list:
    return TEMAS_POR_NIVEL.get(nivel, TEMAS_POR_NIVEL[1])

def _filtrar_temas_relacionados(nivel: int, temas: list) -> list:
    guia = _tokens_significativos(" ".join(TEMAS_POR_NIVEL[nivel]))
    filtrados = []
    for tema in temas:
        if not isinstance(tema, str) or not tema.strip():
            continue
        tokens = _tokens_significativos(tema)
        if tokens & guia:
            filtrados.append(tema.strip())
    return filtrados

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
        temas_filtrados = _filtrar_temas_relacionados(nivel, temas)
        if temas_filtrados:
            print(f"[IA] Temas inferidos (nivel {nivel}): {temas_filtrados}")
            return temas_filtrados
    except Exception as e:
        print(f"[!] inferir_temas nivel {nivel}: error ({e}), usando fallback.")

    return _temas_fallback(nivel)

def pasar_por_critico(pregunta: dict, contexto: str, nivel: int, index: int, dificultad: str, puntaje: int) -> dict:
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
IMPORTANTE: options DEBE ser un array de exactamente 4 strings, nunca objetos ni claves "text".
"""
    explanation_original = str(pregunta.get("explanation", "")).strip()
    contexto_seguro = contexto.encode("utf-8", errors="ignore").decode("utf-8")
    contexto_seguro = re.sub(r'\\u[0-9a-fA-F]{0,3}(?![0-9a-fA-F])', '', contexto_seguro)
    prompt = f"{CRITERIOS}\n\nCONTEXTO:\n{contexto_seguro}\n\nPREGUNTA:\n{json.dumps(pregunta, ensure_ascii=False)}\n\nResponde SOLO con el JSON."

    for intento in range(2):
        try:
            resultado = json.loads(extraer_json(Settings.llm.complete(prompt).text))
            pregunta_final = normalizar_pregunta(
                resultado.get("pregunta_final", pregunta),
                nivel,
                index,
                dificultad,
                puntaje,
            )
            if pregunta_final is None or not validar_pregunta(pregunta_final):
                print("    [CRÍTICO] Estructura inválida tras revisión, usando original")
                return pregunta
            if not str(pregunta_final.get("explanation", "")).strip() and explanation_original:
                pregunta_final["explanation"] = explanation_original
            return pregunta_final
        except Exception as e:
            print(f"    [CRÍTICO] Error intento {intento+1}: {e}")
    return pregunta

def necesita_critico(q: dict) -> bool:
    texto = q.get("question", "")
    opciones = q.get("options", [])
    opciones = [_normalizar_opcion(op) for op in opciones]
    return any([
        "contexto" in texto.lower(),
        "el texto" in texto.lower(),
        not texto.strip().startswith("¿"),
        any(len(op.split()) <= 1 for op in opciones if op),
        len(set(op.lower() for op in opciones)) < 4,
    ])

def ejecutar_pipeline_nivel(nivel: int, ia_state: dict, state_lock) -> list:
    global _idx
    if _idx is None:
        print(f"[!] Error: índice no inicializado (nivel {nivel}).")
        return []

    print(f"\n[IA] Generando preguntas — Nivel {nivel}...")
    dificultades = DIFICULTADES_POR_NIVEL[nivel]
    temas_base = TEMAS_POR_NIVEL[nivel]
    temas_inferidos = inferir_temas_desde_indice(nivel, n_temas=10)
    temas_busqueda = list(dict.fromkeys(temas_base + temas_inferidos))

    preguntas_generadas = []
    conceptos_usados = set()
    stats = {
        "schema_fail": 0,
        "normalization_fail": 0,
        "grounding_fail": 0,
        "duplicate_fail": 0,
        "no_context_fail": 0,
        "accepted": 0,
    }

    total_objetivo = len(dificultades)
    guardadas = 0
    intentos_globales = 0
    MAX_INTENTOS = total_objetivo * 6

    while guardadas < total_objetivo and intentos_globales < MAX_INTENTOS:
        dificultad, puntaje = dificultades[guardadas]
        intentos_globales += 1

        tema_actual = temas_busqueda[(intentos_globales - 1) % len(temas_busqueda)]
        # Aproximamos Parent Retrieval incrementando similarity_top_k para consolidar fragmentos adyacentes de la misma sección
        nodos_brutos = _idx.as_retriever(similarity_top_k=8).retrieve(tema_actual)
        nodos_filtrados = [n for n in nodos_brutos if nodo_util(n.text)]

        if not nodos_filtrados:
            stats["no_context_fail"] += 1
            print(f"[~] Nivel {nivel} — sin contexto útil para tema: {tema_actual}")
            continue

        # Agrupamos un contexto más amplio para asegurar coherencia en explicaciones de código o fórmulas matemáticas
        contexto = "\n\n".join([n.text for n in nodos_filtrados[:4]])
        archivos = sorted({
            n.metadata.get("file_name", "sin_archivo")
            for n in nodos_filtrados[:4]
            if hasattr(n, "metadata")
        })

        # ✓ ADAPTACIÓN PEDAGÓGICA: Incorporamos una condición Low Guidance (Prompt 1 del estudio)
        # Esto incrementa la alineación curricular sin penalizar la flexibilidad adaptativa del LLM
        prompt = f"""
CONTEXTO DE ESTUDIO (Estructuras de Datos y Algoritmos — Nivel {nivel}):
{contexto}

INSTRUCCIONES DE DISEÑO:
- Genera UNA pregunta técnica de opción múltiple basada en el contexto anterior.
- Tema específico objetivo: {tema_actual}
- Nivel de dificultad cognitiva requerido: {dificultad} | Puntaje asignado: {puntaje}
- Usa conceptos, nombres y relaciones presentes en el CONTEXTO DE ESTUDIO; no inventes temas externos.
- Si el contexto incluye fórmulas matemáticas, notación Big-O (ej: O(n log n)) o bloques de código, mantenga su sintaxis matemática intacta tanto en la pregunta como en las opciones.
- PROHIBIDO usar la frase "el contexto proporcionado", "según el texto" o referencias explícitas al documento de lectura.
- PROHIBIDO usar placeholders como "Opción A", "Opción B", "Respuesta correcta" o "Pregunta técnica concreta".
- "options" DEBE ser un array de exactamente 4 strings en español. NO uses objetos, NO uses claves "text", "label" u "option".
- Incluye "explanation": 1 o 2 frases en español que justifiquen por qué la alternativa correcta es la adecuada, basadas en el contexto.
- Responde SOLO con un objeto JSON válido. Sin Markdown, sin texto fuera del JSON, sin listas externas.

JSON REQUERIDO:
{{
    "id": "N{nivel}_{guardadas + 1}_{dificultad}",
    "question": "¿Pregunta técnica basada en el contexto recuperado?",
    "options": ["Alternativa técnica A", "Alternativa técnica B", "Alternativa técnica C", "Alternativa técnica D"],
    "correctAnswerIndex": 0,
    "dificultad": "{dificultad}",
    "puntaje": {puntaje},
    "explanation": "Breve justificación de por qué la alternativa correcta es la adecuada."
}}
"""
        pregunta_valida = None
        for intento in range(3):
            try:
                data = json.loads(extraer_json(Settings.llm.complete(prompt).text))
                normalizada = normalizar_pregunta(data, nivel, guardadas + 1, dificultad, puntaje)
                if normalizada is None:
                    stats["normalization_fail"] += 1
                    continue
                if not validar_pregunta(normalizada):
                    stats["schema_fail"] += 1
                    continue
                if not validar_grounding(normalizada, contexto, tema_actual):
                    stats["grounding_fail"] += 1
                    continue
                pregunta_valida = normalizada
                if archivos:
                    print(f"    [RAG] Nivel {nivel} tema '{tema_actual}' desde {', '.join(archivos[:2])}")
                break
            except Exception as e:
                stats["schema_fail"] += 1
                print(f"    [JSON] ✗ intento {intento + 1}: {e}")

        if not pregunta_valida:
            continue

        contexto_usado = "\n\n".join([n.text for n in nodos_filtrados[:4]])

        if necesita_critico(pregunta_valida):
            pregunta_revisada = pasar_por_critico(
                pregunta_valida,
                contexto_usado,
                nivel,
                guardadas + 1,
                dificultad,
                puntaje,
            )
        else:
            pregunta_revisada = pregunta_valida

        pregunta_revisada = normalizar_pregunta(
            pregunta_revisada,
            nivel,
            guardadas + 1,
            dificultad,
            puntaje,
        )
        if pregunta_revisada is None or not validar_pregunta(pregunta_revisada):
            stats["schema_fail"] += 1
            continue
        if not validar_grounding(pregunta_revisada, contexto_usado, tema_actual):
            stats["grounding_fail"] += 1
            continue

        if any(preguntas_son_similares(pregunta_revisada["question"], q) for q in conceptos_usados):
            stats["duplicate_fail"] += 1
            print(f"[~] Nivel {nivel} — Ciclo {intentos_globales}: duplicada semántica, buscando otra variante...")
            continue

        conceptos_usados.add(pregunta_revisada["question"])
        preguntas_generadas.append(pregunta_revisada)

        with state_lock:
            ia_state["levels"][nivel]["questions"].append(pregunta_revisada)

        guardadas += 1
        stats["accepted"] += 1
        print(f"[✓] Nivel {nivel} — {guardadas}/{total_objetivo}: {pregunta_revisada['question'][:50]}...")

    if guardadas < total_objetivo:
        print(f"[!] Nivel {nivel}: límite alcanzado, generadas {guardadas}/{total_objetivo}.")
    print(f"[IA] Resumen nivel {nivel}: " + ", ".join(f"{k}={v}" for k, v in stats.items()))

    return preguntas_generadas

def ejecutar_pipeline_todos_los_niveles(ia_state: dict, state_lock):
    all_completed = True

    for nivel in [1, 2, 3]:
        with state_lock:
            ia_state["levels"][nivel]["status"] = "generating"
            ia_state["levels"][nivel]["questions"] = []

        preguntas = ejecutar_pipeline_nivel(nivel, ia_state, state_lock)
        expected = len(DIFICULTADES_POR_NIVEL[nivel])
        nivel_completo = len(preguntas) == expected

        with state_lock:
            ia_state["levels"][nivel]["questions"] = preguntas
            ia_state["levels"][nivel]["status"] = "completed" if nivel_completo else "error"

        fname = OUTPUT_DIR / f"preguntas_nivel_{nivel}.json"
        if nivel_completo:
            with open(fname, "w", encoding="utf-8") as f:
                json.dump({"nivel": nivel, "questions": preguntas}, f, ensure_ascii=False, indent=4)
            print(f"[✓] Nivel {nivel} guardado en {fname}")
        else:
            all_completed = False
            print(f"[!] Nivel {nivel}: {len(preguntas)}/{expected} válidas; no se sobrescribe caché con datos incompletos.")

    with state_lock:
        ia_state["status"] = "completed" if all_completed else "error"

    if all_completed:
        save_questions_file(ia_state["levels"])
