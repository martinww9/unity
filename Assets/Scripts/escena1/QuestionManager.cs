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

    // Usamos la ruta base de la API
    private const string BASE_URL = "http://localhost:5000/api";

    private void Awake() => Instance = this;

    public override void Spawned()
    {   
        if (Object.HasStateAuthority)
        {
            // Iniciamos la nueva secuencia de descarga en dos pasos
            StartCoroutine(CheckExistingQuestions());
        }
    }

    public void RetryConnection()
    {
        if (Object.HasStateAuthority) StartCoroutine(GenerateAndDownloadQuestions());
    }

    public void RequestNewGeneration()
    {
        if (Object.HasStateAuthority)
        {
            IsReady = false;
            StartCoroutine(GenerateAndDownloadQuestions());
        }
    }

    private void SincronizarPreguntas(QuestionPool pool)
    {
        RPC_StartSync(pool.questions.Length);
        foreach (var q in pool.questions)
        {
            string o1 = q.options.Length > 0 ? q.options[0] : "";
            string o2 = q.options.Length > 1 ? q.options[1] : "";
            string o3 = q.options.Length > 2 ? q.options[2] : "";
            string o4 = q.options.Length > 3 ? q.options[3] : "";
            RPC_SyncSingleQuestion(q.id, q.text, o1, o2, o3, o4, q.correctAnswerIndex);
        }
    }

    IEnumerator CheckExistingQuestions()
    {
        Debug.Log("IA: Verificando si existen preguntas previas...");
        using (UnityWebRequest webRequest = UnityWebRequest.Get(BASE_URL + "/get-all-questions"))
        {
            yield return webRequest.SendWebRequest();
            
            if (webRequest.result == UnityWebRequest.Result.Success)
            {
                QuestionPool pool = JsonUtility.FromJson<QuestionPool>(webRequest.downloadHandler.text);
                
                if (pool.status == "completed" && pool.questions != null && pool.questions.Length > 0)
                {
                    Debug.Log("IA: Se encontraron preguntas existentes. Cargando...");
                    SincronizarPreguntas(pool);
                }
                else
                {
                    Debug.Log("IA: No hay preguntas listas. Esperando orden de generación.");
                    if (TriviaUI.Instance != null) TriviaUI.Instance.ShowGenerateButton();
                }
            }
            else
            {
                if (TriviaUI.Instance != null) TriviaUI.Instance.OnConnectionError();
            }
        }
    }

    IEnumerator GenerateAndDownloadQuestions()
    {
        // PASO 1: Darle la orden a Python (Flask) para que empiece a pensar
        using (UnityWebRequest webRequest = UnityWebRequest.Get(BASE_URL + "/generate-questions"))
        {
            yield return webRequest.SendWebRequest();
            if (webRequest.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError("Error al contactar a la IA: " + webRequest.error);
                
                // Si Flask está apagado, avisamos a la UI para mostrar el botón Reintentar
                if (TriviaUI.Instance != null) TriviaUI.Instance.OnConnectionError();
                yield break;
            }
            Debug.Log("IA: Generación iniciada en el servidor...");
        }

        // PASO 2: Preguntar periódicamente si ya terminó (Polling)
        bool finished = false;
        while (!finished)
        {
            yield return new WaitForSeconds(3f); // Esperamos 3 segundos entre intentos

            using (UnityWebRequest webRequest = UnityWebRequest.Get(BASE_URL + "/get-all-questions"))
            {
                yield return webRequest.SendWebRequest();
                
                if (webRequest.result == UnityWebRequest.Result.Success)
                {
                    QuestionPool pool = JsonUtility.FromJson<QuestionPool>(webRequest.downloadHandler.text);
                    
                    if (pool.status == "completed" && pool.questions != null)
                    {
                        Debug.Log("IA: ¡Preguntas listas y descargadas!");
                        finished = true; 
                        
                        RPC_StartSync(pool.questions.Length);
                        foreach (var q in pool.questions)
                        {
                            // Creamos variables temporales para las 4 opciones de forma segura
                            string o1 = q.options.Length > 0 ? q.options[0] : "";
                            string o2 = q.options.Length > 1 ? q.options[1] : "";
                            string o3 = q.options.Length > 2 ? q.options[2] : "";
                            string o4 = q.options.Length > 3 ? q.options[3] : "";

                            RPC_SyncSingleQuestion(q.id, q.text, o1, o2, o3, o4, q.correctAnswerIndex);
                        }
                    }
                    else if (pool.status == "error")
                    {
                        Debug.LogError("IA: Hubo un error procesando el PDF en Ollama.");
                        if (TriviaUI.Instance != null) TriviaUI.Instance.OnConnectionError();
                        finished = true; 
                    }
                    else
                    {
                        Debug.Log("IA: Generando... por favor espere.");
                    }
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
            
            // Avisamos a la UI para habilitar el botón de "Iniciar Partida"
            if (TriviaUI.Instance != null) TriviaUI.Instance.OnQuestionsReady();
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
            string o1 = q.options.Length > 0 ? q.options[0] : "";
            string o2 = q.options.Length > 1 ? q.options[1] : "";
            string o3 = q.options.Length > 2 ? q.options[2] : "";
            string o4 = q.options.Length > 3 ? q.options[3] : "";

            RPC_EnviarPreguntaATarget(
                nuevoJugador,
                q.id,
                q.text,
                o1, o2, o3, o4,
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
            
            // Si el jugador acaba de conectarse y la trivia ya está cargada, actualizamos su UI
            if (TriviaUI.Instance != null) TriviaUI.Instance.OnQuestionsReady();
        }
    }
}