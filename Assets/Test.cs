using System.Net.Http;
using System.Text;
using UnityEngine;

public class Test : MonoBehaviour
{
    private readonly string baseUrl = "http://172.16.2.167:8080/";
    //private readonly string baseUrl = "http://127.0.0.1:8080/";
    
    // Start is called before the first frame update
    async void Start()
    {
        TestInstall();
        TestBuild();
    }

    async void TestInstall()
    {
        var handler = new HttpClientHandler();
        handler.ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true;

        using (HttpClient httpClient = new HttpClient(handler))
        {
            string url = baseUrl + "install-editor";

            TestInstallMessage test = new TestInstallMessage
            {
                editor_version = "2021.3.0f1"
            };

            string json = JsonUtility.ToJson(test);

            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
            HttpResponseMessage response = await httpClient.PostAsync(url, content);
            string result = await response.Content.ReadAsStringAsync();
            Debug.Log($"Server response: {result}");
        }
    }

    async void TestBuild()
    {
        var handler = new HttpClientHandler();
        handler.ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true;

        using (HttpClient httpClient = new HttpClient(handler))
        {
            string url = baseUrl + "build-project";

            TestBuildMessage test = new TestBuildMessage
            {
                RepoURL = "https://github.com/SyntaxSnafu/TestBuildPipeline.git",
                CommitHash = "21493b7",
                UnityVersion = "2022.3.62f3",
                BuildTargets = "Windows"
            };

            string json = JsonUtility.ToJson(test);

            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
            HttpResponseMessage response = await httpClient.PostAsync(url, content);
            string result = await response.Content.ReadAsStringAsync();
            Debug.Log($"Server response: {result}");
        }
    }
}

[System.Serializable]
public class TestInstallMessage
{
    public string editor_version;
}

[System.Serializable]
public class TestBuildMessage
{
    public string RepoURL;
    public string CommitHash;
    public string UnityVersion;
    public string BuildTargets;
}