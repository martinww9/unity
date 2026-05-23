# app.py
import sys, pathlib, os
import json
import threading
from flask import Flask, jsonify, request
from jsonschema import validate, ValidationError

from src import rag_pipeline

app = Flask(__name__)

state_lock = threading.Lock()
feedback_lock = threading.Lock()
ia_state = {
    "status": "indexing", 
    "questions": []
}

# Directorio para guardar los JSON de feedback de los jugadores
LLM_DIR = pathlib.Path(__file__).parent.parent / "llm_data"
FEEDBACK_DIR = LLM_DIR / "llm_feedback"

os.makedirs(FEEDBACK_DIR, exist_ok=True)

def inicializar_sistema_bg():
    global ia_state
    try:
        rag_pipeline.cargar_o_crear_index()
        print("[4/4] ¡Índice cargado! Sistema IA listo.")
        with state_lock:
            ia_state["status"] = "idle" 
    except Exception as e:
        print(f"Error crítico al inicializar el índice: {e}")
        with state_lock:
            ia_state["status"] = "error"

# =====================================================================
# ENDPOINTS DE PREGUNTAS (QUESTIONS)
# =====================================================================

@app.route('/api/questions/generate', methods=['POST'])
def questions_generate():
    datos = request.get_json() or {}
    trivia_id = datos.get("trivia_id", "default")
    archivo_salida = f"preguntas_{trivia_id}.json"

    with state_lock:
        estado_actual = ia_state["status"]
        
    if estado_actual == "indexing": 
        return jsonify({"status": "indexing", "message": "Procesando archivos..."})
    if estado_actual == "generating": 
        return jsonify({"status": "generating", "message": "Trabajando..."})
    
    # Comprobar si ya existe el JSON para ese identificador
    if os.path.exists(archivo_salida):
        try:
            with open(archivo_salida, "r", encoding="utf-8") as f:
                data = json.load(f)
                preguntas_guardadas = data.get("questions", [])
                
            if preguntas_guardadas:
                with state_lock:
                    ia_state["questions"] = preguntas_guardadas
                    ia_state["status"] = "completed"
                return jsonify({"status": "completed", "message": f"Preguntas cargadas desde {archivo_salida}."})
        except Exception as e:
            print(f"⚠️ Error leyendo caché, se generarán de nuevo: {e}")

    # Iniciar generación
    with state_lock:
        ia_state["status"] = "generating"
        ia_state["questions"] = []
        
    thread = threading.Thread(
        target=rag_pipeline.ejecutar_pipeline_preguntas, 
        args=(ia_state, state_lock, archivo_salida)
    )
    thread.start()
    return jsonify({"status": "started", "message": f"Generación iniciada para ID: {trivia_id}."})

@app.route('/api/questions/get', methods=['GET'])
def questions_get_all():
    with state_lock:
        estado = ia_state["status"]
        lista_preguntas = list(ia_state["questions"])
        
    if estado != "completed":
        return jsonify({"status": estado, "questions": []})
    return jsonify({"status": "completed", "questions": lista_preguntas})

@app.route('/api/questions/get/<question_id>', methods=['GET'])
def questions_get_single(question_id):
    with state_lock:
        lista_preguntas = list(ia_state["questions"])
        
    for q in lista_preguntas:
        if str(q.get("id")) == str(question_id):
            return jsonify(q)
            
    return jsonify({"error": "Pregunta no encontrada en el pool activo"}), 404

# =====================================================================
# ENDPOINTS DE FEEDBACK
# =====================================================================

@app.route('/api/feedback/generate/<player_id>', methods=['POST'])
def feedback_generate(player_id):
    datos = request.get_json() or {}
    puntaje = datos.get("score", 0)
    total = datos.get("total", 10)
    historial = datos.get("historial", []) # Nuevo: Array con las respuestas elegidas por el jugador
    
    archivo_feedback = os.path.join(FEEDBACK_DIR, f"feedback_{player_id}.json")
    
    try:
        with feedback_lock:
            # Ahora le enviamos también el historial de lo que el jugador respondió
            resultado_feedback = rag_pipeline.ejecutar_pipeline_feedback(puntaje, total, historial)
        
        validate(instance=resultado_feedback, schema=rag_pipeline.SCHEMA_FEEDBACK)
        
        # Guardamos el resultado en disco asociado a su identificador
        with open(archivo_feedback, "w", encoding="utf-8") as f:
            json.dump(resultado_feedback, f, ensure_ascii=False, indent=4)
            
        return jsonify(resultado_feedback)
    except ValidationError as e:
        return jsonify({"error": f"Feedback con formato inválido: {e.message}"}), 500
    except Exception as e:
        return jsonify({"error": str(e)}), 500

@app.route('/api/feedback/get/<player_id>', methods=['GET'])
def feedback_get(player_id):
    archivo_feedback = os.path.join(FEEDBACK_DIR, f"feedback_{player_id}.json")
    
    if not os.path.exists(archivo_feedback):
        return jsonify({"error": f"No existe feedback generado para el jugador: {player_id}"}), 404
        
    try:
        with open(archivo_feedback, "r", encoding="utf-8") as f:
            data = json.load(f)
        return jsonify(data)
    except Exception as e:
        return jsonify({"error": f"Error al leer el archivo: {str(e)}"}), 500

if __name__ == '__main__':
    rag_pipeline.configurar_modelos()
    hilo_inicio = threading.Thread(target=inicializar_sistema_bg)
    hilo_inicio.start()
    print("🚀 Servidor Flask API REST activo...")
    app.run(host='0.0.0.0', port=5000, debug=True, use_reloader=False)