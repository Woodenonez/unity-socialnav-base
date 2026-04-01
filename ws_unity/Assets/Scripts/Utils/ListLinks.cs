using UnityEngine;
using System.IO;
using System.Text;

public class ExportRobotLinksToTXT : MonoBehaviour
{
    public Transform robotRoot;

    void Start()
    {
        if (robotRoot == null)
            robotRoot = transform;

        StringBuilder sb = new StringBuilder();

        foreach (Transform t in robotRoot.GetComponentsInChildren<Transform>(true))
        {
            if (t == robotRoot)
                continue;

            sb.AppendLine(t.name);
        }

        // Save inside project folder
        string path = Path.Combine(Application.dataPath, "RobotLinks.txt");

        File.WriteAllText(path, sb.ToString());

        Debug.Log("TXT file created at: " + path);
    }
}