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
                    // Creamos variables temporales para las 4 opciones
                    string o1 = q.options.Length > 0 ? q.options[0] : "";
                    string o2 = q.options.Length > 1 ? q.options[1] : "";
                    string o3 = q.options.Length > 2 ? q.options[2] : "";
                    string o4 = q.options.Length > 3 ? q.options[3] : "";

                    RPC_SyncSingleQuestion(
                        q.id, 
                        q.text, 
                        o1, o2, o3, o4, // Enviamos las variables seguras
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

public void SincronizarConNuevoJugador(PlayerRef nuevoJugador)
    {
        // Solo el Host tiene los datos del LLM y debe enviarlos
        if (!Object.HasStateAuthority || !IsReady || _questions == null) return;

        Debug.Log($"[Host] Sincronizando trivia con el jugador: {nuevoJugador.PlayerId}");

        // 1. Enviamos señal de inicio con el total de preguntas
        RPC_EnviarInicioATarget(nuevoJugador, _questions.Length);

        // 2. Enviamos cada pregunta individualmente
        foreach (var q in _questions)
        {
            RPC_EnviarPreguntaATarget(
                nuevoJugador,
                q.id,
                q.text,
                q.options[0], q.options[1], q.options[2], q.options[3],
                q.correctAnswerIndex
            );
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RPC_EnviarInicioATarget([RpcTarget] PlayerRef target, int totalQuestions)
    {
        // Esta lógica solo se ejecuta en el cliente 'target'
        _questionsList.Clear();
        _questions = new Question[totalQuestions];
        IsReady = false;
        Debug.Log($"[Cliente] Recibiendo trivia dirigida. Total: {totalQuestions}");
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RPC_EnviarPreguntaATarget([RpcTarget] PlayerRef target, string id, string text, string o1, string o2, string o3, string o4, int correct)
    {
        // Esta lógica solo se ejecuta en el cliente 'target'
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
            Debug.Log("[Cliente] Trivia dirigida recibida y lista.");
        }
    }

}