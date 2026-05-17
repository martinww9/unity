# app.py
import threading
from flask import Flask, jsonify, request
from jsonschema import validate, ValidationError

# Importamos las funciones aisladas de nuestro pipeline modular
import rag_pipeline

app = Flask(__name__)

# Control thread-safe de concurrencia y estado de la IA
state_lock = threading.Lock()
feedback_lock = threading.Lock()
ia_state = {
    "status": "indexing", 
    "questions": []
}

def inicializar_sistema_bg():
    """Hilo secundario para arrancar el RAG al encender el servidor sin bloquear a Flask."""
    global ia_state
    try:
        # 1. Lee, limpia e indexa los PDFs de la carpeta 'data'
        rag_pipeline.cargar_o_crear_index()
        print("[4/4] ¡Índice cargado! Sistema IA listo.")
        with state_lock:
            ia_state["status"] = "idle" 
    except Exception as e:
        print(f"Error crítico al inicializar el índice: {e}")
        with state_lock:
            ia_state["status"] = "error"

# === ENDPOINTS DE LA API REST ===

@app.route('/api/generate-questions', methods=['GET', 'POST'])
def run_generate_questions():
    with state_lock:
        estado_actual = ia_state["status"]
        
    if estado_actual == "indexing": 
        return jsonify({"status": "indexing", "message": "Procesando archivos..."})
    if estado_actual == "generating": 
        return jsonify({"status": "generating", "message": "Trabajando..."})
    
    # Reiniciar lista temporal bajo lock seguro
    with state_lock:
        ia_state["status"] = "generating"
        ia_state["questions"] = []
        
    # Lanzar la tarea de generación iterativa en segundo plano
    thread = threading.Thread(
        target=rag_pipeline.ejecutar_pipeline_preguntas, 
        args=(ia_state, state_lock)
    )
    thread.start()
    return jsonify({"status": "started", "message": "Proceso iterativo bilingüe iniciado."})

@app.route('/api/generate-feedback', methods=['POST'])
def run_generate_feedback():
    datos = request.get_json() or {}
    puntaje = datos.get("score", 0)
    total = datos.get("total", 10)
    
    try:
        with feedback_lock:
            resultado_feedback = rag_pipeline.ejecutar_pipeline_feedback(puntaje, total)
        
        # Validar antes de devolver
        validate(instance=resultado_feedback, schema=rag_pipeline.SCHEMA_FEEDBACK)
        
        return jsonify(resultado_feedback)
    except ValidationError as e:
        return jsonify({"error": f"Feedback con formato inválido: {e.message}"}), 500
    except Exception as e:
        return jsonify({"error": str(e)}), 500

@app.route('/api/get-all-questions', methods=['GET'])
def run_get_all_questions():
    with state_lock:
        estado = ia_state["status"]
        lista_preguntas = list(ia_state["questions"])
        
    if estado != "completed":
        return jsonify({"status": estado, "questions": []})
    return jsonify({"status": "completed", "questions": lista_preguntas})

@app.route('/api/get-question/<int:index>', methods=['GET'])
def run_get_question(index):
    with state_lock:
        estado = ia_state["status"]
        lista_preguntas = list(ia_state["questions"])
        
    if estado != "completed":
        return jsonify({"error": "No listas", "status": estado}), 400
    if index < 0 or index >= len(lista_preguntas):
        return jsonify({"error": "Fuera de rango"}), 404
        
    return jsonify(lista_preguntas[index])

if __name__ == '__main__':
    # 1. Configurar LLM y Embeddings en el Hilo Principal (Crucial para LlamaIndex)
    rag_pipeline.configurar_modelos()
    
    # 2. Iniciar el indexado en segundo plano
    hilo_inicio = threading.Thread(target=inicializar_sistema_bg)
    hilo_inicio.start()
    
    # 3. Arrancar servidor Flask inmediato
    print("🚀 Servidor Flask activo en hilos controlados...")
    app.run(host='0.0.0.0', port=5000, debug=True, use_reloader=False)