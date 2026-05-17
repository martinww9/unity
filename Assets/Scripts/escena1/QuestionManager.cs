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

    private const string BASE_URL = "https://relocate-dismount-scorecard.ngrok-free.dev:5000/api";

    private void Awake() => Instance = this;

    public override void Spawned()
    {   
        if (Object.HasStateAuthority)
        {
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
            
            RPC_SyncSingleQuestion(q.id, q.question, o1, o2, o3, o4, q.correctAnswerIndex, q.dificultad, q.puntaje);
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
        using (UnityWebRequest webRequest = UnityWebRequest.Get(BASE_URL + "/generate-questions"))
        {
            yield return webRequest.SendWebRequest();
            if (webRequest.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError("Error al contactar a la IA: " + webRequest.error);
                if (TriviaUI.Instance != null) TriviaUI.Instance.OnConnectionError();
                yield break;
            }
            Debug.Log("IA: Generación iniciada en el servidor...");
        }

        bool finished = false;
        while (!finished)
        {
            yield return new WaitForSeconds(3f);

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
                            string o1 = q.options.Length > 0 ? q.options[0] : "";
                            string o2 = q.options.Length > 1 ? q.options[1] : "";
                            string o3 = q.options.Length > 2 ? q.options[2] : "";
                            string o4 = q.options.Length > 3 ? q.options[3] : "";

                            // CORRECCIÓN: q.question en vez de q.text
                            RPC_SyncSingleQuestion(q.id, q.question, o1, o2, o3, o4, q.correctAnswerIndex, q.dificultad, q.puntaje);
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
    public void RPC_SyncSingleQuestion(string id, string question, string o1, string o2, string o3, string o4, int correct, string dificultad, int puntaje)
    {
        // Forzamos un fallback por si acaso llega a viajar algo nulo
        Question q = new Question {
            id = string.IsNullOrEmpty(id) ? "0" : id,
            question = string.IsNullOrEmpty(question) ? "Pregunta Corrupta" : question,
            options = new string[] { o1, o2, o3, o4 },
            correctAnswerIndex = correct,
            dificultad = string.IsNullOrEmpty(dificultad) ? "Fácil" : dificultad,
            puntaje = puntaje == 0 ? 10 : puntaje
        };

        _questionsList.Add(q);

        if (_questionsList.Count == _questions.Length)
        {
            _questions = _questionsList.ToArray();
            IsReady = true;
            Debug.Log("Trivia sincronizada.");
            if (TriviaUI.Instance != null) TriviaUI.Instance.OnQuestionsReady();
        }
    }

    public Question GetQuestion(int index)
    {
        if (_questions != null && index >= 0 && index < _questions.Length)
            return _questions[index];
        return null;
    }

    public void SincronizarConNuevoJugador(PlayerRef nuevoJugador)
    {
        if (!Object.HasStateAuthority || !IsReady || _questions == null || _questions.Length == 0)
        {        
            Debug.LogWarning("[QuestionManager] Preguntas no listas aún.");
            return;
        }
        Debug.Log($"[Host] Sincronizando trivia con el jugador: {nuevoJugador.PlayerId}");

        if (nuevoJugador == Runner.LocalPlayer) return;
        RPC_EnviarInicioATarget(nuevoJugador, _questions.Length);

        foreach (var q in _questions)
        {
            string o1 = q.options.Length > 0 ? q.options[0] : "";
            string o2 = q.options.Length > 1 ? q.options[1] : "";
            string o3 = q.options.Length > 2 ? q.options[2] : "";
            string o4 = q.options.Length > 3 ? q.options[3] : "";

            RPC_EnviarPreguntaATarget(nuevoJugador, q.id, q.question, o1, o2, o3, o4, q.correctAnswerIndex, q.dificultad, q.puntaje);
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RPC_EnviarInicioATarget([RpcTarget] PlayerRef target, int totalQuestions)
    {
        if (Object.HasStateAuthority) return;
        _questionsList.Clear();
        _questions = new Question[totalQuestions];
        IsReady = false;
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RPC_EnviarPreguntaATarget([RpcTarget] PlayerRef target, string id, string question, string o1, string o2, string o3, string o4, int correct, string dificultad, int puntaje)
    {
        if (Object.HasStateAuthority) return;
        
        Question q = new Question {
            id = id,
            question = question,
            options = new string[] { o1, o2, o3, o4 },
            correctAnswerIndex = correct,
            dificultad = dificultad,
            puntaje = puntaje
        };

        _questionsList.Add(q);

        if (_questionsList.Count == _questions.Length)
        {
            _questions = _questionsList.ToArray();
            IsReady = true;
            if (TriviaUI.Instance != null) TriviaUI.Instance.OnQuestionsReady();
        }
    }

    public void SolicitarFeedbackFinal(int score, int total)
    {
        // Ejecutamos la petición HTTP por corrutina al terminar la partida
        StartCoroutine(PostFeedbackRoutine(score, total));
    }

    private IEnumerator PostFeedbackRoutine(int score, int total)
    {
        string url = BASE_URL + "/generate-feedback";
        
        // Estructuramos el payload exactamente como lo requiere request.get_json() en app.py
        FeedbackRequest payload = new FeedbackRequest { score = score, total = total };
        string jsonBody = JsonUtility.ToJson(payload);

        using (UnityWebRequest webRequest = new UnityWebRequest(url, "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonBody);
            webRequest.uploadHandler = new UploadHandlerRaw(bodyRaw);
            webRequest.downloadHandler = new DownloadHandlerBuffer();
            webRequest.SetRequestHeader("Content-Type", "application/json");

            webRequest.timeout = 300;

            yield return webRequest.SendWebRequest();

            if (webRequest.result == UnityWebRequest.Result.Success)
            {
                FeedbackData data = JsonUtility.FromJson<FeedbackData>(webRequest.downloadHandler.text);
                Debug.Log("IA: Planilla de Feedback generada y validada.");
                
                if (TriviaUI.Instance != null)
                {
                    TriviaUI.Instance.ShowFeedback(data);
                }
            }
            else
            {
                Debug.LogError("IA: Error al procesar la retroalimentación: " + webRequest.error);
            }
        }
    }
}