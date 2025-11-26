#if UNITY_EDITOR

using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace com.bloc.BuildPipeline.Editor
{
    public class BuildPipelineUI : EditorWindow
    {
        private readonly string baseUrl = "http://10.0.10.34:8080/";
        //private readonly string baseUrl = "http://127.0.0.1:8080/";
    
        private readonly string buildAPIEndpoint = "api/build-project";
        private readonly string buildStatusAPIEndpoint = "api/build-status";
    
        [SerializeField]
        private VisualTreeAsset m_VisualTreeAsset = default;

        private TextField repoGitTextField;
        private TextField commitHashTextField;
        private TextField unityVersionTextField;
        private TextField unityChangesetTextField;
        private Toggle windowsToggle;
        private Toggle androidToggle;
        private Toggle iOSToggle;
        private Toggle webGLToggle;

        private Button refreshDefaultsButton;
        private Button buildButton;
        private ProgressBar buildProgressBar;

        private Label errorMessageLabel;

        private string buildID = "";
    
        private CancellationTokenSource cancellationTokenSource;

        [MenuItem("Tools/Build Pipeline")]
        public static void ShowExample()
        {
            BuildPipelineUI wnd = GetWindow<BuildPipelineUI>();
            wnd.titleContent = new GUIContent("Build Pipeline");
        }

        public void CreateGUI()
        {
            // Each editor window contains a root VisualElement object
            VisualElement root = rootVisualElement;

            // Instantiate UXML
            VisualElement labelFromUXML = m_VisualTreeAsset.Instantiate();
            root.Add(labelFromUXML);
        
            repoGitTextField = root.Q<TextField>("RepoInputField");
            commitHashTextField = root.Q<TextField>("CommitHashInputField");
            unityVersionTextField = root.Q<TextField>("UnityVersionInputField");
            unityChangesetTextField = root.Q<TextField>("UnityChangesetInputField");
            windowsToggle = root.Q<Toggle>("WindowsToggle");
            androidToggle = root.Q<Toggle>("AndroidToggle");
            iOSToggle = root.Q<Toggle>("iOSToggle");
            webGLToggle = root.Q<Toggle>("WebGLToggle");
        
            refreshDefaultsButton = root.Q<Button>("RefreshDefaultsButton");
            buildButton = root.Q<Button>("BuildButton");
            buildProgressBar = root.Q<ProgressBar>("BuildProgressBar");
            
            errorMessageLabel = root.Q<Label>("ErrorMessageLabel");
        
            errorMessageLabel.visible = false;
            buildProgressBar.visible = false;
            buildProgressBar.tooltip = "Updates every 30 seconds. Be Patient";

            FetchAndApplyDefaults();
        
            refreshDefaultsButton.clicked += FetchAndApplyDefaults;
            buildButton.clicked += BuildButtonOnClicked;
        }

        private void FetchAndApplyDefaults()
        {
            string projectPath = Directory.GetParent(Application.dataPath).FullName;

            if (projectPath == "" || projectPath == null) return;

            // Set repo path
            string gitConfigPath = Path.Combine(projectPath, ".git", "config");
            string gitConfig = File.ReadAllText(gitConfigPath);
            
            string[] lines = gitConfig.Split('\n');
            foreach (string line in lines)
            {
                if (line.Trim().StartsWith("url = "))
                {
                    string repoUrl = line.Trim().Substring(6).Trim();
                    repoGitTextField.value = repoUrl;
                    break;
                }
            }
            
            // Set commit hash
            string commitHashPath = Path.Combine(projectPath, ".git", "HEAD");
            string commitHash = File.ReadAllText(commitHashPath);
            if (commitHash.StartsWith("ref:"))
            {
                string refPath = commitHash.Substring(5).Trim();
                string fullRefPath = Path.Combine(projectPath, ".git", refPath);
                
                commitHash = File.ReadAllText(fullRefPath).Trim();
            }
            commitHashTextField.value = commitHash;
            
            // Set Unity Version
            string projectVersionPath = Path.Combine(projectPath, "ProjectSettings", "ProjectVersion.txt");
            string projectVersionFile = File.ReadAllText(projectVersionPath);
            string[] versionLines = projectVersionFile.Split('\n');
            foreach (string line in versionLines)
            {
                if (line.StartsWith("m_EditorVersion: "))
                {
                    string unityVersion = line.Substring(17).Trim();
                    unityVersionTextField.value = unityVersion;
                }
                else if (line.StartsWith("m_EditorVersionWithRevision: "))
                {
                    int startIndex = line.IndexOf("(");
                    int endIndex = line.IndexOf(")");
                    if (startIndex == -1 || endIndex == -1) continue;
                    string changeset = line.Substring(startIndex + 1, endIndex - startIndex - 1);
                    unityChangesetTextField.value = changeset;
                }
            }
        }
        
        private void TriggerErrorMessage(string message)
        {
            errorMessageLabel.text = message;
            errorMessageLabel.visible = true;
        }

        private async void BuildButtonOnClicked()
        {
            if (repoGitTextField.text.Length <= 0)
            {
                TriggerErrorMessage("Please enter a valid repository git repository URL.");
                return;
            }

            if (commitHashTextField.text.Length <= 0)
            {
                TriggerErrorMessage("Please enter a valid commit hash.");
                return;
            }

            if (unityVersionTextField.text.Length <= 0)
            {
                TriggerErrorMessage("Please enter a valid unity version.");
                return;
            }
            string platforms = "";
            if (windowsToggle.value) platforms += "Windows,";
            if (androidToggle.value) platforms += "Android,";
            if (iOSToggle.value) platforms += "iOS,";
            if (webGLToggle.value) platforms += "WebGL,";
            if (platforms.Length <= 0)
            {
                TriggerErrorMessage("Please select at least one platform to build.");
                return;
            }
            platforms = platforms.TrimEnd(',');
            errorMessageLabel.visible = false;

            BuildConfig config = new BuildConfig
            {
                RepoURL = repoGitTextField.text,
                CommitHash = commitHashTextField.text,
                UnityVersion = unityVersionTextField.text,
                UnityChangeset = unityChangesetTextField.text,
                BuildTargets = platforms
            };
        
            buildID = await SendBuildMessage(config);
            
            if (cancellationTokenSource != null) cancellationTokenSource.Cancel();
            cancellationTokenSource = new CancellationTokenSource();
            _ = PollProgressUpdate(cancellationTokenSource.Token);
            
        
            buildProgressBar.visible = true;
            buildProgressBar.value = 1f;
            buildProgressBar.title = "Starting Process...";
        }

        private async Task PollProgressUpdate(CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    await Task.Delay(TimeSpan.FromSeconds(5), token); // Poll every 10 seconds
                    
                    BuildStatus status = null;
                    try
                    {
                        status = await GetBuildStatus(buildID);
                        buildProgressBar.title = status.Message;
                        buildProgressBar.value = status.Progress;

                        switch (status.StatusCode)
                        {
                            case -1:
                                Debug.LogError("Build failed");
                                cancellationTokenSource.Cancel();
                                break;
                            case 0:
                                break;
                            case 1:
                                Debug.Log("Build finished");
                                cancellationTokenSource.Cancel();
                                break;
                            case 400:
                                Debug.LogError("Build failed with client error.");
                                cancellationTokenSource.Cancel();
                                break;
                            case 500:
                                Debug.LogError("Build failed with server error.");
                                cancellationTokenSource.Cancel();
                                break;
                            case 501:
                                Debug.LogWarning("Build Completed with server error.");
                                break;
                            default:
                                Debug.LogWarning("Unknown status code: " + status.StatusCode);
                                Debug.LogWarning("Please contact Cole");
                                break;
                        }
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning("Failed to get build status: " + e.Message);
                        break;
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("Failed to get build status: " + e.Message);
            }
        }
    
        private async Task<string> SendBuildMessage(BuildConfig config)
        {
            var handler = new HttpClientHandler();
            handler.ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true;

            using (HttpClient httpClient = new HttpClient(handler))
            {
                string url = baseUrl + buildAPIEndpoint;

                string json = JsonUtility.ToJson(config);

                var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
                HttpResponseMessage response = await httpClient.PostAsync(url, content);
                string result = await response.Content.ReadAsStringAsync();
                Debug.Log($"Server response: {result}");
                BuildIDMessage buildIDMessage = JsonUtility.FromJson<BuildIDMessage>(result);
                return buildIDMessage.BuildID;
            }
        }
        
        private async Task<BuildStatus> GetBuildStatus(string buildID)
        {
            var handler = new HttpClientHandler();
            handler.ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true;

            using (HttpClient httpClient = new HttpClient(handler))
            {
                string url = baseUrl + buildStatusAPIEndpoint;

                BuildIDMessage buildIDMessage = new BuildIDMessage
                {
                    BuildID = buildID
                };
                string json = JsonUtility.ToJson(buildIDMessage);
                Debug.Log("Before sending");
                Debug.Log(buildID);
                Debug.Log(json);
                Debug.Log(url);

                var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
                HttpResponseMessage response = await httpClient.PostAsync(url, content);
                string result = await response.Content.ReadAsStringAsync();
                Debug.Log($"Server response: {result}");
                BuildStatus status = JsonUtility.FromJson<BuildStatus>(result);
                return status;
            }
        }
    }

    [System.Serializable]
    public class BuildConfig
    {
        public string RepoURL;
        public string CommitHash;
        public string UnityVersion;
        public string UnityChangeset;
        public string BuildTargets;
    }

    [System.Serializable]
    public class BuildStatus
    {
        public int BuildID;
        public string Message;
        public int StatusCode;
        public int Progress;
    }

    [System.Serializable]
    public class BuildIDMessage
    {
        public string BuildID;
    }
}

#endif