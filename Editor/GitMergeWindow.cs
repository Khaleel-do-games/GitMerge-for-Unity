﻿using UnityEngine;
using UnityEditor;
using System.IO;
using System.Diagnostics;
using System.Collections.Generic;

public class GitMergeWindow : EditorWindow
{
    private static string git = @"C:\Program Files (x86)\Git\bin\git.exe";
    private static List<GitMergeActions> allMergeActions;

    private static string sceneName;
    private static string theirSceneName;
    private static List<GameObject> addedObjects;


	[MenuItem("Window/GitMerge")]
    static void OpenEditor()
    {
        EditorWindow.GetWindow(typeof(GitMergeWindow), true, "GitMerge");
    }

    void OnGUI()
    {
        GUILayout.Label("Open Scene: "+EditorApplication.currentScene);
        if(GUILayout.Button("Do Stuff"))
        {
            GetTheirVersionOf(EditorApplication.currentScene);

            var ourObjects = GetAllSceneObjects();
            EditorApplication.OpenSceneAdditive(theirSceneName);
            addedObjects = GetAllNewSceneObjects(ourObjects);
            Hide(addedObjects);

            BuildAllMergeActions(ourObjects, addedObjects);
        }

        var done = false;
        if(allMergeActions != null)
        {
            done = true;
            foreach(var actions in allMergeActions)
            {
                actions.OnGUI();
                done = done && actions.merged;
            }
        }
        if(done && GUILayout.Button("Done!"))
        {
            CompleteMerge();
        }
    }

    private static List<GameObject> GetAllSceneObjects()
    {
        return new List<GameObject>((GameObject[])FindObjectsOfType(typeof(GameObject)));
    }

    private static List<GameObject> GetAllNewSceneObjects(List<GameObject> oldObjects)
    {
        var all = GetAllSceneObjects();
        var old = oldObjects;

        foreach(var obj in old)
        {
            all.Remove(obj);
        }

        return all;
    }

    private void Hide(List<GameObject> objects)
    {
        foreach(var obj in objects)
        {
            obj.hideFlags = HideFlags.HideAndDontSave;
        }
    }

    private static void GetTheirVersionOf(string path)
    {
        sceneName = path;

        string basepath = Path.GetDirectoryName(path);
        string sname = Path.GetFileNameWithoutExtension(path);

        string ours = Path.Combine(basepath, sname + "--OURS.unity");
        theirSceneName = Path.Combine(basepath, sname + "--THEIRS.unity");

        File.Copy(path, ours);
        ExecuteGit("checkout --theirs " + path);
        File.Move(path, theirSceneName);
        File.Move(ours, path);
    }

    private void BuildAllMergeActions(List<GameObject> ourObjects, List<GameObject> theirObjects)
    {
        allMergeActions = new List<GitMergeActions>();

        var theirObjectsDict = new Dictionary<int, GameObject>();
        foreach(var theirs in theirObjects)
        {
            theirObjectsDict.Add(ObjectIDFinder.GetIdentifierFor(theirs), theirs);
        }

        foreach(var ours in ourObjects)
        {
            var id = ObjectIDFinder.GetIdentifierFor(ours);
            GameObject theirs;
            theirObjectsDict.TryGetValue(id, out theirs);

            var mergeActions = new GitMergeActions(ours, theirs);
            if(mergeActions.hasActions)
            {
                allMergeActions.Add(mergeActions);
            }
            theirObjectsDict.Remove(id);
        }
        
        foreach(var theirs in theirObjectsDict.Values)
        {
            //new GameObjects from them
            var mergeActions = new GitMergeActions(null, theirs);
            if(mergeActions.hasActions)
            {
                allMergeActions.Add(mergeActions);
            }
        }
    }

    private static void CompleteMerge()
    {
        foreach(var obj in addedObjects)
        {
            DestroyImmediate(obj);
        }
        EditorApplication.SaveScene();
        AssetDatabase.DeleteAsset(theirSceneName);

        allMergeActions = null;

        if(EditorUtility.DisplayDialog("Commit Merge", "Do you want to commit the merge now?", "Commit Now", "Commit Later"))
        {
            ExecuteGit("reset HEAD");
            ExecuteGit("add " + sceneName);
            ExecuteGit("commit -m \"Merged "+sceneName+".\"");
        }
    }

    private static string ExecuteGit(string args)
    {
        var process = new Process();
        var startInfo = new ProcessStartInfo();
        startInfo.WindowStyle = ProcessWindowStyle.Hidden;
        startInfo.FileName = git;
        startInfo.Arguments = args;
        startInfo.UseShellExecute = false;
        startInfo.RedirectStandardOutput = true;
        process.StartInfo = startInfo;

        process.Start();

        string output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();

        return output;
    }

    private static void print(string msg)
    {
        UnityEngine.Debug.Log(msg);
    }
}