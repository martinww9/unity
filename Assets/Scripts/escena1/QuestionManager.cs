using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Collections.Generic;
using Fusion;

public class QuestionManager : NetworkBehaviour
{
    public static QuestionManager Instance;
    private List<Question> _questionsList = new List<Question>();
    private Question[] _questions;
    public bool IsReady { get; private set; }

    private void Awake() => Instance = this;

    public override void Spawned()
    {   
        if (Object.HasStateAuthority){
        StartCoroutine(DownloadQuestions());
        }
    }

    IEnumerator DownloadQuestions()
    {
        string url = "http://localhost:5000/api/get-questions"; 
        using (UnityWebRequest webRequest = UnityWebRequest.Get(url))
        {
            yield return webRequest.SendWebRequest();
            if (webRequest.result == UnityWebRequest.Result.Success)
            {
                Debug.Log("Preguntas descargadas exitosamente.");
                QuestionPool pool = JsonUtility.FromJson<QuestionPool>(webRequest.downloadHandler.text);
                RPC_StartSync(pool.questions.Length);
                
                foreach (var q in pool.questions)
                {
                RPC_SyncSingleQuestion(
                                    q.id, 
                                    q.text, 
                                    q.options[0], q.options[1], q.options[2], q.options[3], 
                                    q.correctAnswerIndex
                );
                }
         }
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        public void RPC_StartSync(int totalQuestions)
        {
            _questionsList.Clear();
            _questions = new Question[totalQuestions];
            IsReady = false;
            Debug.Log($"IA: Iniciando recepción de {totalQuestions} preguntas...");
        }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RPC_SyncSingleQuestion(string id, string text, string o1, string o2, string o3, string o4, int correct)
    {
        Question q = new Question {
            id = id,
            text = text,
            options = new string[] { o1, o2, o3, o4 },
            correctAnswerIndex = correct
        };

        _questionsList.Add(q);

        if (_questionsList.Count == _questions.Length)
        {
            _questions = _questionsList.ToArray();
            IsReady = true;
            Debug.Log("Trivia sincronizada.");
        }
    }

    public Question GetQuestion(int index)
    {
        // Añadir validación index >= 0
        if (_questions != null && index >= 0 && index < _questions.Length)
            return _questions[index];
        return null;
    }
}