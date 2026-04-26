using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using Fusion;

public class QuestionManager : NetworkBehaviour
{
    public static QuestionManager Instance;
    private Question[] _questions;
    public bool IsReady { get; private set; }

    private void Awake() => Instance = this;

    public override void Spawned()
    {
        StartCoroutine(DownloadQuestions());
    }

    IEnumerator DownloadQuestions()
    {
        // Simulamos una pequeña espera de red
    yield return new WaitForSeconds(1f);

    // Creamos preguntas de prueba manualmente
    _questions = new Question[]
    {
        new Question { text = "¿Cuál es la capital de Francia?", options = new string[]{"París", "Londres", "Madrid", "Roma"}, correctAnswerIndex = 0 },
        new Question { text = "¿2 + 2?", options = new string[]{"3", "4", "5", "6"}, correctAnswerIndex = 1 },
        new Question { text = "¿Cuál es la capital de Francia?", options = new string[]{"París", "Londres", "Madrid", "Roma"}, correctAnswerIndex = 0 },
    };
    
    IsReady = true;
    Debug.Log("Modo de prueba: Preguntas cargadas localmente.");

        /*
        // Placeholder: Aquí pondrás la URL de tu API de Ollama/RAG
        string url = "http://tu-backend-ia/api/get-questions"; 
        
        using (UnityWebRequest webRequest = UnityWebRequest.Get(url))
        {
            yield return webRequest.SendWebRequest();

            if (webRequest.result == UnityWebRequest.Result.Success)
            {
                QuestionPool pool = JsonUtility.FromJson<QuestionPool>(webRequest.downloadHandler.text);
                _questions = pool.questions;
                IsReady = true;
                Debug.Log("Pool de preguntas cargado con éxito.");
            }
        }
        */
    }

    public Question GetQuestion(int index)
    {
        // Añadir validación index >= 0
        if (_questions != null && index >= 0 && index < _questions.Length)
            return _questions[index];
        return null;
    }
}