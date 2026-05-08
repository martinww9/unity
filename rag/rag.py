import os
import json
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

# Configuración de Ollama (tu código original)
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

@app.route('/api/get-questions', methods=['GET'])
def get_questions_api():
    query_engine = idx.as_query_engine(similarity_top_k=8)
    
    prompt = f"""
    Eres un experto en evaluación técnica. Genera 10 preguntas basadas en el PDF.

    REGLAS CRÍTICAS DE TAMAÑO:
    - El texto de la 'text' NO DEBE superar los 120 caracteres.
    - Cada una de las 'options' NO DEBE superar los 40 caracteres.
    - Sé muy conciso para evitar errores de red.

    DEBES devolver un objeto JSON:
    {{
    "questions": [
        {{
        "id": "string único corto",
        "text": "pregunta corta",
        "options": ["opt1", "opt2", "opt3", "opt4"],
        "correctAnswerIndex": número del 0 al 3
        }}
    ]
    }}
    Responde solo con el JSON.
    """
    
    response = query_engine.query(prompt)
    # Retornamos el JSON directamente a Unity
    return jsonify(json.loads(response.response))

if __name__ == "__main__":
    app.run(host='0.0.0.0', port=5000)