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
        Actúa como el Profesor titular experto de la asignatura. Has leído el documento proporcionado sobre "Detección de Vulnerabilidades con LLMs" y debes evaluar a tus alumnos a través de un juego de trivia interactivo.

        Tu tarea es generar exactamente 10 preguntas desafiantes basadas ÚNICAMENTE en la información de este documento.

        REGLAS CRÍTICAS (Si no las cumples, el sistema del juego fallará):
        1. LONGITUD DE PREGUNTA: El 'text' NO puede superar los 120 caracteres.
        2. LONGITUD DE OPCIONES: Debes crear exactamente 4 'options'. Ninguna opción debe superar los 45 caracteres.
        3. DIFICULTAD: Crea 1 respuesta correcta y 3 distractores (respuestas incorrectas) que suenen creíbles y técnicas.
        4. ÍNDICE: El 'correctAnswerIndex' debe ser un número entero (0, 1, 2 o 3) apuntando a la opción correcta.

        ESTRUCTURA OBLIGATORIA:
        DEBES devolver ÚNICAMENTE un objeto JSON válido. No incluyas saludos, confirmaciones, ni texto en markdown fuera del JSON. Utiliza exactamente este formato:

        {
          "questions": [
            {
              "id": "q1_ejemplo",
              "text": "¿Cuál es la principal ventaja de usar un LLM para analizar código fuente?",
              "options": ["Velocidad de ejecución", "Comprensión del contexto", "Menor uso de memoria", "Reemplazo del compilador"],
              "correctAnswerIndex": 1
            }
          ]
        }
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