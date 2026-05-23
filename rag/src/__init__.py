import sys, pathlib
ROOT_DIR = pathlib.Path(__file__).parent
LLM_DIR = ROOT_DIR.parent / "llm_data"
sys.path.insert(0, str(ROOT_DIR))
sys.path.insert(0, str(LLM_DIR))
print(f"Working on: {ROOT_DIR}")
print(f"LLM Directory: {LLM_DIR}")