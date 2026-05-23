import os
import time
import threading
from flask import Flask, jsonify

app = Flask(__name__)

print("========================================")
print("🚀 MODO DESARROLLO: Servidor Flask Rápido (Sin IA)")
print("========================================")

# === PREGUNTAS HARDCODEADAS ===
# Puedes agregar, quitar o editar las preguntas aquí directamente
PREGUNTAS_ESTATICAS = [
    {
        "id": "q1",
        "text": "¿Cuál es la principal ventaja de usar un LLM para analizar código fuente?",
        "options": ["Velocidad de ejecución", "Comprensión del contexto", "Menor uso de memoria", "Reemplazo del compilador"],
        "correctAnswerIndex": 1
    },
    {
        "id": "q2",
        "text": "¿Qué significa la sigla RAG en inteligencia artificial?",
        "options": ["Random Access Generator", "Retrieval-Augmented Gen", "Rapid AI Growth", "Real-time AI Graphics"],
        "correctAnswerIndex": 1
    },
    {
        "id": "q3",
        "text": "¿Qué modelo estamos utilizando localmente para este proyecto?",
        "options": ["GPT-4", "Claude 3", "Llama 3.1", "Gemini"],
        "correctAnswerIndex": 2
    },
    {
        "id": "q4",
        "text": "¿Qué componente de Unity se usa para detectar si el jugador cruzó la meta?",
        "options": ["Rigidbody", "Box Collider (Is Trigger)", "Mesh Filter", "Network Object"],
        "correctAnswerIndex": 1
    },
    {
        "id": "q5",
        "text": "¿Cuál es la principal ventaja de usar un LLM para analizar código fuente?",
        "options": ["Velocidad de ejecución", "Comprensión del contexto", "Menor uso de memoria", "Reemplazo del compilador"],
        "correctAnswerIndex": 1
    },
    {
        "id": "q6",
        "text": "¿Qué significa la sigla RAG en inteligencia artificial?",
        "options": ["Random Access Generator", "Retrieval-Augmented Gen", "Rapid AI Growth", "Real-time AI Graphics"],
        "correctAnswerIndex": 1
    },
    {
        "id": "q7",
        "text": "¿Qué modelo estamos utilizando localmente para este proyecto?",
        "options": ["GPT-4", "Claude 3", "Llama 3.1", "Gemini"],
        "correctAnswerIndex": 2
    },
    {
        "id": "q8",
        "text": "¿Qué componente de Unity se usa para detectar si el jugador cruzó la meta?",
        "options": ["Rigidbody", "Box Collider (Is Trigger)", "Mesh Filter", "Network Object"],
        "correctAnswerIndex": 1
    }
]

ia_state = {
    "status": "idle", 
    "questions": []
}

def simular_generacion_ia():
    """Simula que la IA está pensando por un segundo y luego carga las preguntas hardcodeadas."""
    global ia_state
    ia_state["status"] = "generating"
    
    print("[IA] Simulando generación de preguntas...")
    time.sleep(1.5) # Un pequeño retraso para que en Unity se alcance a ver el "IA Pensando..."
    
    ia_state["questions"] = PREGUNTAS_ESTATICAS
    ia_state["status"] = "completed"
    print("[IA] ✅ Preguntas estáticas cargadas con éxito.")

# === ENDPOINTS ===

@app.route('/api/generate-questions', methods=['GET', 'POST'])
def generate_questions():
    """Inicia el proceso de 'generación' (que ahora es instantáneo)."""
    global ia_state
    
    if ia_state["status"] == "generating":
        return jsonify({"status": "generating", "message": "Trabajando..."})
    
    thread = threading.Thread(target=simular_generacion_ia)
    thread.start()
    
    return jsonify({"status": "started", "message": "Cargando preguntas locales."})


@app.route('/api/get-all-questions', methods=['GET'])
def get_all_questions():
    """Devuelve las preguntas estáticas a Unity."""
    global ia_state
    
    if ia_state["status"] != "completed":
        return jsonify({"status": ia_state["status"], "questions": []})
        
    return jsonify({
        "status": "completed", 
        "questions": ia_state["questions"]
    })

if __name__ == '__main__':
    app.run(host='0.0.0.0', port=5000, debug=True, use_reloader=False)