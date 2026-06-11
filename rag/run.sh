#!/bin/bash
cd /home/ratin/Escritorio/proyectoahorasiquesi/unity/rag/
nohup uv run python -m src.app > flask.log 2>&1 &
nohup ngrok http 5000 > ngrok.log 2>&1 &
