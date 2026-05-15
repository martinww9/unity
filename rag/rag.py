import os
import json
import threading
from flask import Flask, jsonify
from llama_index.core import (
    VectorStoreIndex, 
    SimpleDirectoryReader, 
    Settings, 
    StorageContext, 
    load_index_from_storage
)
from llama_index.embeddings.ollama import OllamaEmbedding
from llama_index.llms.ollama import Ollama

app = Flask(__name__)

def configurar_modelos():
    """Configura los modelos solo una vez en el hilo principal."""
    print("[1/4] Configurando modelo LLM (llama3.1:latest)...")
    llm = Ollama(
        model="llama3.1:latest", 
        request_timeout=600.0,
        temperature=0.8,
        format="json", 
        additional_kwargs={"num_ctx": 8192} 
    )

    print("[2/4] Configurando modelo de Embeddings (nomic-embed-text)...")
    Settings.embed_model = OllamaEmbedding(model_name="nomic-embed-text")
    Settings.llm = llm
    
    # Optimizaciones de chunks que agregamos antes
    Settings.chunk_size = 512       
    Settings.chunk_overlap = 50

# Variables globales para guardar el índice y el estado
idx = None
ia_state = {
    "status": "indexing", # NUEVO ESTADO: 'indexing', 'idle', 'generating', 'completed', 'error'
    "questions": []
}

"""def get_index(data_dir="data", storage_dir="storage"):
    if not os.path.exists(storage_dir):
        print(f"[3/4] No se encontró un índice previo en '{storage_dir}'. Creando uno nuevo en segundo plano...")
        if not os.path.exists(data_dir): os.makedirs(data_dir)
        documents = SimpleDirectoryReader(data_dir).load_data()
        index = VectorStoreIndex.from_documents(documents)
        index.storage_context.persist(persist_dir=storage_dir)
    else:
        print(f"[3/4] Cargando el índice existente desde '{storage_dir}' en segundo plano...")
        storage_context = StorageContext.from_defaults(persist_dir=storage_dir)
        index = load_index_from_storage(storage_context)
    return index
"""
def get_index(data_dir="data", storage_dir="storage"):
    # 1. Aseguramos que el directorio de datos exista
    if not os.path.exists(data_dir):
        print(f"      -> Creando directorio '{data_dir}'. ¡Asegúrate de poner tus PDFs ahí!")
        os.makedirs(data_dir)
        
    # OPTIMIZACIÓN 1: Lectura paralela (Usa 4 hilos para leer múltiples PDFs más rápido)
    print(f"      -> Leyendo documentos desde '{data_dir}'...")
    documents = SimpleDirectoryReader(data_dir).load_data(num_workers=6)

    if not os.path.exists(storage_dir):
        # Si no hay índice, creamos uno desde cero
        print(f"[3/4] No se encontró un índice previo. Creando desde cero...")
        
        # OPTIMIZACIÓN 2: show_progress=True muestra una barra de carga en la consola
        index = VectorStoreIndex.from_documents(documents, show_progress=True)
        index.storage_context.persist(persist_dir=storage_dir)
    else:
        # Si ya existe, lo cargamos a la memoria
        print(f"[3/4] Cargando el índice existente desde '{storage_dir}'...")
        storage_context = StorageContext.from_defaults(persist_dir=storage_dir)
        index = load_index_from_storage(storage_context)
        
        # OPTIMIZACIÓN 3: Actualización Incremental Inteligente
        print("      -> Verificando si hay documentos nuevos o modificados...")
        
        # refresh_ref_docs compara los hashes de los archivos. 
        # Solo procesa (embbedings) los PDFs que hayas agregado o modificado.
        cambios_realizados = index.refresh_ref_docs(documents, show_progress=True)
        
        if any(cambios_realizados):
            print("      -> ¡Cambios detectados! Guardando el índice actualizado...")
            index.storage_context.persist(persist_dir=storage_dir)
        else:
            print("      -> El índice ya está 100% actualizado.")
            
    return index

def inicializar_sistema():
    """Esta función corre en segundo plano al arrancar Flask para no bloquear el servidor."""
    global idx, ia_state
    try:
        idx = get_index()
        print("[4/4] ¡Índice cargado! Sistema IA listo para recibir peticiones.")
        ia_state["status"] = "idle" # Ya terminamos de indexar, estamos listos
    except Exception as e:
        print(f"Error al inicializar el índice: {e}")
        ia_state["status"] = "error"

def tarea_generar_preguntas():
    """Esta función corre en segundo plano para generar las preguntas."""
    global ia_state, idx
    ia_state["status"] = "generating"
    ia_state["questions"] = []
    
    print("[IA] Generando preguntas...")
    
    try:
        query_engine = idx.as_query_engine(similarity_top_k=8)
        
        prompt = """
        Actúa como el Profesor titular experto de la asignatura. Has leído los documentos proporcionados y debes evaluar a tus alumnos a través de un juego de trivia interactivo.

        Tu tarea es generar exactamente 10 preguntas desafiantes basadas ÚNICAMENTE en la información de este documento.

        REGLAS CRÍTICAS:
        - El texto de la 'text' NO DEBE superar los 120 caracteres.
        - Cada una de las 'options' NO DEBE superar los 40 caracteres.
        - Sé muy conciso para evitar errores de red.

        DEBES devolver un objeto JSON:
        {
            "questions": [
                {
                    "id": "string único corto",
                    "text": "pregunta corta",
                    "options": ["opt1", "opt2", "opt3", "opt4"],
                    "correctAnswerIndex": número del 0 al 3
                }
            ]
        }
        Responde solo con el archivo .json.
        """
        
        response = query_engine.query(prompt)
        
        # Limpieza de la respuesta corregida (evita el error 'list object has no attribute split')
        raw_text = response.response
        if "```json" in raw_text:
            raw_text = raw_text.split("```json").split("```").strip()
        elif "```" in raw_text:
            raw_text = raw_text.split("```").strip()
            
        data = json.loads(raw_text)
        
        ia_state["questions"] = data.get("questions", [])
        ia_state["status"] = "completed"
        print("Preguntas generadas con éxito")
        
    except Exception as e:
        print(f"Error al generar preguntas: {e}")
        ia_state["status"] = "error"


# === ENDPOINTS ===

@app.route('/api/generate-questions', methods=['GET', 'POST'])
def generate_questions():
    """Inicia el proceso de generación de preguntas."""
    global ia_state
    
    # 1. Si aún está procesando los PDFs al abrir el servidor
    if ia_state["status"] == "indexing":
        return jsonify({"status": "indexing", "message": "El servidor está procesando los PDFs. Intenta en unos segundos..."})
    
    # 2. Si ya está pensando en las preguntas
    if ia_state["status"] == "generating":
        return jsonify({"status": "generating", "message": "La IA ya está trabajando..."})
    
    # 3. Si está libre, lo ponemos a trabajar en otro hilo
    thread = threading.Thread(target=tarea_generar_preguntas)
    thread.start()
    
    return jsonify({"status": "started", "message": "Generación iniciada en segundo plano."})


@app.route('/api/get-all-questions', methods=['GET'])
def get_all_questions():
    """Devuelve todas las preguntas si ya están listas."""
    global ia_state
    
    if ia_state["status"] != "completed":
        return jsonify({"status": ia_state["status"], "questions": []})
        
    return jsonify({
        "status": "completed", 
        "questions": ia_state["questions"]
    })


@app.route('/api/get-question/<int:index>', methods=['GET'])
def get_question(index):
    """Devuelve una pregunta específica por su índice (0 a 9)."""
    global ia_state
    
    if ia_state["status"] != "completed":
        return jsonify({"error": "Las preguntas aún no están listas.", "status": ia_state["status"]}), 400
        
    if index < 0 or index >= len(ia_state["questions"]):
        return jsonify({"error": "Índice de pregunta fuera de rango."}), 404
        
    return jsonify(ia_state["questions"][index])

if __name__ == '__main__':
    # Arrancamos la creación del índice en un hilo aparte justo antes de iniciar Flask
    hilo_inicio = threading.Thread(target=inicializar_sistema)
    hilo_inicio.start()
    
    print("🚀 Iniciando el servidor Flask de inmediato...")
    app.run(host='0.0.0.0', port=5000, debug=True, use_reloader=False) # use_reloader=False evita que se ejecute el hilo dos veces