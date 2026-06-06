# app.py
import threading
from flask import Flask, jsonify
from . import rag_pipeline

app = Flask(__name__)

state_lock = threading.Lock()

ia_state = {
    "status": "indexing",
    "levels": {
        1: {"status": "idle", "questions": []},
        2: {"status": "idle", "questions": []},
        3: {"status": "idle", "questions": []},
    },
}


def inicializar_sistema_bg():
    global ia_state
    try:
        rag_pipeline.cargar_o_crear_index()
        print("[4/4] Índice cargado. Sistema IA listo.")
        with state_lock:
            if rag_pipeline.cargar_cache_preguntas_en_estado(ia_state):
                ia_state["status"] = "completed"
                print("[RAG] Caché de preguntas restaurada (3 niveles).")
            else:
                ia_state["status"] = "idle"
    except Exception as e:
        print(f"Error crítico al inicializar el índice: {e}")
        with state_lock:
            ia_state["status"] = "error"


def _status_payload():
    payload = rag_pipeline.build_questions_payload(ia_state["levels"])
    return {
        "status": ia_state["status"] if ia_state["status"] in ("indexing", "generating") else payload["status"],
        "levels": {
            str(level["nivel"]): {
                "status": level["status"],
                "count": len(level["questions"]),
            }
            for level in payload["levels"]
        },
    }


def _questions_file_payload():
    payload = rag_pipeline.build_questions_payload(ia_state["levels"])
    if ia_state["status"] in ("indexing", "generating"):
        payload["status"] = ia_state["status"]
    return payload


def _questions_for_level(nivel):
    if nivel not in [1, 2, 3]:
        return None
    nivel_state = ia_state["levels"][nivel]
    return {
        "nivel": nivel,
        "status": nivel_state["status"],
        "questions": list(nivel_state["questions"]),
    }


@app.route("/api/questions/generate", methods=["GET", "POST"])
def run_generate_questions():
    with state_lock:
        estado_actual = ia_state["status"]

    if estado_actual == "indexing":
        return jsonify({"status": "indexing", "message": "Procesando archivos..."})
    if estado_actual == "generating":
        return jsonify({"status": "generating", "message": "Ya hay una generación en curso..."})

    with state_lock:
        ia_state["status"] = "generating"
        for nivel in [1, 2, 3]:
            ia_state["levels"][nivel]["status"] = "generating"
            ia_state["levels"][nivel]["questions"] = []

    thread = threading.Thread(
        target=rag_pipeline.ejecutar_pipeline_todos_los_niveles,
        args=(ia_state, state_lock),
    )
    thread.start()
    return jsonify({"status": "started", "message": "Generando los 3 sets de preguntas..."})


@app.route("/api/questions/status", methods=["GET"])
@app.route("/api/get-all-levels-status", methods=["GET"])
def run_questions_status():
    with state_lock:
        return jsonify(_status_payload())


@app.route("/api/questions/all", methods=["GET"])
def run_get_all_questions():
    with state_lock:
        return jsonify(_questions_file_payload())


@app.route("/api/questions/<int:nivel>", methods=["GET"])
@app.route("/api/questions/get/<int:nivel>", methods=["GET"])
def run_get_questions_nivel(nivel):
    with state_lock:
        payload = _questions_for_level(nivel)
    if payload is None:
        return jsonify({"error": "Nivel inválido. Usa 1, 2 o 3."}), 400
    return jsonify(payload)


if __name__ == "__main__":
    rag_pipeline.configurar_modelos()

    hilo_inicio = threading.Thread(target=inicializar_sistema_bg)
    hilo_inicio.start()

    print("Servidor Flask activo...")
    app.run(host="0.0.0.0", port=5000, debug=True, use_reloader=False)
