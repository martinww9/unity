// QuestionManager.cs corregido para Producción en Red Externa
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

    // ✓ CORRECCIÓN 1: Se eliminó el puerto :5000 redundante para ngrok
    // private const string BASE_URL = "https://relocate-dismount-scorecard.ngrok-free.dev/api";
    private const string BASE_URL = "localhost:5000/api"; // Para pruebas locales, ngrok se encargará de redirigir correctamente
    
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
        using (UnityWebRequest webRequest = UnityWebRequest.Get(BASE_URL + "/questions/get"))
        {
            // ✓ CORRECCIÓN 2: Encabezado crítico para evadir la validación HTML de ngrok
            webRequest.SetRequestHeader("ngrok-skip-browser-warning", "69420");
            
            yield return webRequest.SendWebRequest();
            
            if (webRequest.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    QuestionPool pool = JsonUtility.FromJson<QuestionPool>(webRequest.downloadHandler.text);
                    
                    if (pool != null && pool.status == "completed" && pool.questions != null && pool.questions.Length > 0)
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
                catch (System.Exception e)
                {
                    Debug.LogError("IA: Error parseando JSON de respuesta. Posible HTML corrupto: " + e.Message);
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
        string url = BASE_URL + "/questions/generate";
        
        // Creamos el payload en JSON con el identificador de la trivia (útil si luego quieres tener múltiples salas)
        string jsonBody = "{\"trivia_id\": \"default\"}"; 

        using (UnityWebRequest webRequest = new UnityWebRequest(url, "POST"))
        {
            // Convertimos el string a bytes y lo inyectamos en el cuerpo del POST
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonBody);
            webRequest.uploadHandler = new UploadHandlerRaw(bodyRaw);
            webRequest.downloadHandler = new DownloadHandlerBuffer();
            
            // Cabeceras estrictas
            webRequest.SetRequestHeader("Content-Type", "application/json");
            webRequest.SetRequestHeader("ngrok-skip-browser-warning", "69420");
            
            yield return webRequest.SendWebRequest();
            
            if (webRequest.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError("Error al contactar a la IA: " + webRequest.error);
                if (TriviaUI.Instance != null) TriviaUI.Instance.OnConnectionError();
                yield break;
            }
            Debug.Log("IA: Generación iniciada en el servidor...");
        }

        // El ciclo de Polling con GET se mantiene igual, ya que el endpoint /questions/get sí es GET
        bool finished = false;
        while (!finished)
        {
            yield return new WaitForSeconds(3f);

            using (UnityWebRequest webRequest = UnityWebRequest.Get(BASE_URL + "/questions/get"))
            {
                webRequest.SetRequestHeader("ngrok-skip-browser-warning", "69420");
                yield return webRequest.SendWebRequest();
                
                if (webRequest.result == UnityWebRequest.Result.Success)
                {
                    try
                    {
                        QuestionPool pool = JsonUtility.FromJson<QuestionPool>(webRequest.downloadHandler.text);
                        
                        if (pool != null && pool.status == "completed" && pool.questions != null)
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

                                RPC_SyncSingleQuestion(q.id, q.question, o1, o2, o3, o4, q.correctAnswerIndex, q.dificultad, q.puntaje);
                            }
                        }
                        else if (pool != null && pool.status == "error")
                        {
                            Debug.LogError("IA: Hubo un error procesando el PDF en Ollama.");
                            if (TriviaUI.Instance != null) TriviaUI.Instance.OnConnectionError();
                            finished = true; 
                        }
                    }
                    catch
                    {
                        Debug.LogWarning("IA: Esperando estructura JSON limpia...");
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

    public void SolicitarFeedbackFinal(string playerId, int score, int total)
    {
        StartCoroutine(PostFeedbackRoutine(playerId, score, total));
    }

    private IEnumerator PostFeedbackRoutine(string playerId, int score, int total)
    {
        // ✓ CONFIGURADO: Endpoint exacto solicitado: api/feedback/generate/{id}
        string url = BASE_URL + "/feedback/generate" + "/" + playerId;
        
        FeedbackRequest payload = new FeedbackRequest { score = score, total = total };
        string jsonBody = JsonUtility.ToJson(payload);

        using (UnityWebRequest webRequest = new UnityWebRequest(url, "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonBody);
            webRequest.uploadHandler = new UploadHandlerRaw(bodyRaw);
            webRequest.downloadHandler = new DownloadHandlerBuffer();
            webRequest.SetRequestHeader("Content-Type", "application/json");
            webRequest.SetRequestHeader("ngrok-skip-browser-warning", "69420");

            webRequest.timeout = 300; // 5 minutos de margen para Ollama

            yield return webRequest.SendWebRequest();

            if (webRequest.result == UnityWebRequest.Result.Success)
            {
                FeedbackData data = JsonUtility.FromJson<FeedbackData>(webRequest.downloadHandler.text);
                Debug.Log($"IA: Feedback guardado en el servidor para {playerId} y descargado en Unity.");
                
                if (TriviaUI.Instance != null)
                {
                    TriviaUI.Instance.ShowFeedback(data);
                }
            }
            else
            {
                Debug.LogError("IA: Error al procesar la retroalimentación: " + webRequest.error);
                if (TriviaUI.Instance != null) TriviaUI.Instance.DesbloquearBotonPorError();
            }
        }
    }
}