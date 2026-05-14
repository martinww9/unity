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

# Configuración de Ollama
llm = Ollama(
    model="llama3.1:latest", 
    request_timeout=600.0,
    temperature=0.3,
    format="json", 
    additional_kwargs={"num_ctx": 8192} 
)
Settings.embed_model = OllamaEmbedding(model_name="nomic-embed-text")
Settings.llm = llm

def get_index(data_dir="data", storage_dir="storage"):
    if not os.path.exists(storage_dir):
        if not os.path.exists(data_dir): os.makedirs(data_dir)
        documents = SimpleDirectoryReader(data_dir).load_data()
        index = VectorStoreIndex.from_documents(documents)
        index.storage_context.persist(persist_dir=storage_dir)
    else:
        storage_context = StorageContext.from_defaults(persist_dir=storage_dir)
        index = load_index_from_storage(storage_context)
    return index

# Inicializamos el índice al arrancar el servidor
idx = get_index()

# === NUEVA ARQUITECTURA DE ESTADO ===
# Variables globales para guardar el estado de la IA y las preguntas
ia_state = {
    "status": "idle", # Puede ser: 'idle', 'generating', 'completed', 'error'
    "questions": []
}

def tarea_generar_preguntas():
    """Esta función corre en segundo plano para no bloquear el servidor Flask."""
    global ia_state
    ia_state["status"] = "generating"
    ia_state["questions"] = []
    
    try:
        query_engine = idx.as_query_engine(similarity_top_k=8)
        
        prompt = """
        Eres un experto en evaluación técnica. Genera 10 preguntas basadas en el PDF.

        REGLAS CRÍTICAS DE TAMAÑO:
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
        Responde solo con el JSON validado.
        """
        
        response = query_engine.query(prompt)
        
        # Limpieza de la respuesta por si Ollama añade markdown (```json ... ```)
        raw_text = response.response
        if "```json" in raw_text:
            raw_text = raw_text.split("```json").split("```").strip()
        elif "```" in raw_text:
            raw_text = raw_text.split("```").strip()
            
        data = json.loads(raw_text)
        
        ia_state["questions"] = data.get("questions", [])
        ia_state["status"] = "completed"
        print("¡Preguntas generadas con éxito en segundo plano!")
        
    except Exception as e:
        print(f"Error al generar preguntas: {e}")
        ia_state["status"] = "error"


# === ENDPOINTS ===

@app.route('/api/generate-questions', methods=['GET', 'POST'])
def generate_questions():
    """Inicia el proceso de generación de preguntas."""
    global ia_state
    
    if ia_state["status"] == "generating":
        return jsonify({"status": "generating", "message": "La IA ya está trabajando..."})
    
    # Lanzamos la tarea en un hilo separado para que Flask responda de inmediato
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
    app.run(host='0.0.0.0', port=5000, debug=True)