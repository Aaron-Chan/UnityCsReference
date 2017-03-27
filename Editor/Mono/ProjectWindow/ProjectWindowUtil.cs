// Unity C# reference source
// Copyright (c) Unity Technologies. For terms of use, see
// https://unity3d.com/legal/licenses/Unity_Reference_Only_License

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor.Audio;
using UnityEditor.ProjectWindowCallback;
using UnityEditor.SceneManagement;
using UnityEditorInternal;
using UnityEngine;
using Object = UnityEngine.Object;

namespace UnityEditor
{
    internal class DragAndDropDelay
    {
        public Vector2 mouseDownPosition;

        public bool CanStartDrag()
        {
            return Vector2.Distance(mouseDownPosition, Event.current.mousePosition) > 6;
        }
    }

    // Callbacks to be used when creating assets via the project window
    // You can extend the EndNameEditAction and write your own callback
    // It is done this way instead of via a delegate because the action
    // needs to survive an assembly reload.
    namespace ProjectWindowCallback
    {
        public abstract class EndNameEditAction : ScriptableObject
        {
            public virtual void OnEnable()
            {
                hideFlags = HideFlags.HideAndDontSave;
            }

            public abstract void Action(int instanceId, string pathName, string resourceFile);

            public virtual void CleanUp()
            {
                DestroyImmediate(this);
            }
        }

        internal class DoCreateNewAsset : EndNameEditAction
        {
            public override void Action(int instanceId, string pathName, string resourceFile)
            {
                AssetDatabase.CreateAsset(EditorUtility.InstanceIDToObject(instanceId),
                    AssetDatabase.GenerateUniqueAssetPath(pathName));
                ProjectWindowUtil.FrameObjectInProjectWindow(instanceId);
            }
        }

        internal class DoCreateFolder : EndNameEditAction
        {
            public override void Action(int instanceId, string pathName, string resourceFile)
            {
                string guid = AssetDatabase.CreateFolder(Path.GetDirectoryName(pathName), Path.GetFileName(pathName));
                Object o = AssetDatabase.LoadAssetAtPath(AssetDatabase.GUIDToAssetPath(guid), typeof(Object));
                ProjectWindowUtil.ShowCreatedAsset(o);
            }
        }

        internal class DoCreateScene : EndNameEditAction
        {
            public override void Action(int instanceId, string pathName, string resourceFile)
            {
                bool createDefaultGameObjects = true;
                if (EditorSceneManager.CreateSceneAsset(pathName, createDefaultGameObjects))
                {
                    Object sceneAsset = AssetDatabase.LoadAssetAtPath(pathName, typeof(SceneAsset));
                    ProjectWindowUtil.ShowCreatedAsset(sceneAsset);
                }
            }
        }

        internal class DoCreatePrefab : EndNameEditAction
        {
            public override void Action(int instanceId, string pathName, string resourceFile)
            {
                Object o = PrefabUtility.CreateEmptyPrefab(pathName);
                ProjectWindowUtil.ShowCreatedAsset(o);
            }
        }

        internal class DoCreateScriptAsset : EndNameEditAction
        {
            public override void Action(int instanceId, string pathName, string resourceFile)
            {
                Object o = ProjectWindowUtil.CreateScriptAssetFromTemplate(pathName, resourceFile);
                ProjectWindowUtil.ShowCreatedAsset(o);
            }
        }

        internal class DoCreateAnimatorController : EndNameEditAction
        {
            public override void Action(int instanceId, string pathName, string resourceFile)
            {
                Animations.AnimatorController controller = Animations.AnimatorController.CreateAnimatorControllerAtPath(pathName);
                ProjectWindowUtil.ShowCreatedAsset(controller);
            }
        }

        internal class DoCreateAudioMixer : EndNameEditAction
        {
            public override void Action(int instanceId, string pathName, string resourceFile)
            {
                AudioMixerController controller = AudioMixerController.CreateMixerControllerAtPath(pathName);

                // Check if the output group should be initialized (instanceID is stored in the resource file) TODO: rename 'resourceFile' to 'userData' so it's more obvious that it can be used by all EndNameEditActions
                if (!string.IsNullOrEmpty(resourceFile))
                {
                    int outputInstanceID;
                    if (System.Int32.TryParse(resourceFile, out outputInstanceID))
                    {
                        var outputGroup = InternalEditorUtility.GetObjectFromInstanceID(outputInstanceID) as AudioMixerGroupController;
                        if (outputGroup != null)
                            controller.outputAudioMixerGroup = outputGroup;
                    }
                }
                ProjectWindowUtil.ShowCreatedAsset(controller);
            }
        }

        internal class DoCreateSpritePolygon : EndNameEditAction
        {
            public int sides;
            public override void Action(int instanceId, string pathName, string resourceFile)
            {
                bool showSpriteEditorAfter = false;
                if (sides < 0)
                {
                    sides = 5;
                    showSpriteEditorAfter = true;
                }

                Sprites.SpriteUtility.CreateSpritePolygonAssetAtPath(pathName, sides);
                if (showSpriteEditorAfter)
                {
                    Selection.activeObject = AssetDatabase.LoadMainAssetAtPath(pathName);
                    SpriteEditorWindow.GetWindow();
                }
            }
        }
    }

    public class ProjectWindowUtil
    {
        [MenuItem("Assets/Create/GUI Skin", false, 601)]
        public static void CreateNewGUISkin()
        {
            GUISkin skin = ScriptableObject.CreateInstance<GUISkin>();
            GUISkin original = Resources.GetBuiltinResource(typeof(GUISkin), "GameSkin/GameSkin.guiskin") as GUISkin;
            if (original)
                EditorUtility.CopySerialized(original, skin);
            else
                Debug.LogError("Internal error: unable to load builtin GUIskin");

            CreateAsset(skin, "New GUISkin.guiskin");
        }

        // Returns the path of currently selected folder. If multiple are selected, returns the first one.
        internal static string GetActiveFolderPath()
        {
            ProjectBrowser projectBrowser = GetProjectBrowserIfExists();

            if (projectBrowser == null)
                return "Assets";

            return projectBrowser.GetActiveFolderPath();
        }

        internal static void EndNameEditAction(EndNameEditAction action, int instanceId, string pathName, string resourceFile)
        {
            pathName = AssetDatabase.GenerateUniqueAssetPath(pathName);
            if (action != null)
            {
                action.Action(instanceId, pathName, resourceFile);
                action.CleanUp();
            }
        }

        // Create a standard Object-derived asset.
        public static void CreateAsset(Object asset, string pathName)
        {
            StartNameEditingIfProjectWindowExists(asset.GetInstanceID(), ScriptableObject.CreateInstance<DoCreateNewAsset>(), pathName, AssetPreview.GetMiniThumbnail(asset), null);
        }

        // Create a folder
        public static void CreateFolder()
        {
            StartNameEditingIfProjectWindowExists(0, ScriptableObject.CreateInstance<DoCreateFolder>(), "New Folder", EditorGUIUtility.IconContent(EditorResourcesUtility.emptyFolderIconName).image as Texture2D, null);
        }

        public static void CreateScene()
        {
            StartNameEditingIfProjectWindowExists(0, ScriptableObject.CreateInstance<DoCreateScene>(), "New Scene.unity", EditorGUIUtility.FindTexture("SceneAsset Icon") as Texture2D, null);
        }

        // Create a prefab
        public static void CreatePrefab()
        {
            StartNameEditingIfProjectWindowExists(0, ScriptableObject.CreateInstance<DoCreatePrefab>(), "New Prefab.prefab", EditorGUIUtility.IconContent("Prefab Icon").image as Texture2D, null);
        }

        static void CreateScriptAsset(string templatePath, string destName)
        {
            var templateFilename = Path.GetFileName(templatePath);
            if (templateFilename.ToLower().Contains("editortest") || templateFilename.ToLower().Contains("editmode"))
            {
                var tempPath =  AssetDatabase.GetUniquePathNameAtSelectedPath(destName);
                if (!tempPath.ToLower().Contains("/editor/"))
                {
                    tempPath = tempPath.Substring(0, tempPath.Length - destName.Length - 1);
                    var editorDirPath = Path.Combine(tempPath, "Editor");
                    if (!Directory.Exists(editorDirPath))
                        AssetDatabase.CreateFolder(tempPath, "Editor");
                    tempPath = Path.Combine(editorDirPath, destName);
                    tempPath = tempPath.Replace("\\", "/");
                }
                destName = tempPath;
            }

            Texture2D icon = null;
            switch (Path.GetExtension(destName))
            {
                case ".js":
                    icon = EditorGUIUtility.IconContent("js Script Icon").image as Texture2D;
                    break;
                case ".cs":
                    icon = EditorGUIUtility.IconContent("cs Script Icon").image as Texture2D;
                    break;
                case ".boo":
                    icon = EditorGUIUtility.IconContent("boo Script Icon").image as Texture2D;
                    break;
                case ".shader":
                    icon = EditorGUIUtility.IconContent("Shader Icon").image as Texture2D;
                    break;
                default:
                    icon = EditorGUIUtility.IconContent("TextAsset Icon").image as Texture2D;
                    break;
            }
            StartNameEditingIfProjectWindowExists(0, ScriptableObject.CreateInstance<DoCreateScriptAsset>(), destName, icon, templatePath);
        }

        public static void ShowCreatedAsset(Object o)
        {
            // Show it
            Selection.activeObject = o;
            if (o)
                FrameObjectInProjectWindow(o.GetInstanceID());
        }

        static private void CreateAnimatorController()
        {
            var icon = EditorGUIUtility.IconContent("AnimatorController Icon").image as Texture2D;
            StartNameEditingIfProjectWindowExists(0, ScriptableObject.CreateInstance<DoCreateAnimatorController>(), "New Animator Controller.controller", icon, null);
        }

        static private void CreateAudioMixer()
        {
            var icon = EditorGUIUtility.IconContent("AudioMixerController Icon").image as Texture2D;
            StartNameEditingIfProjectWindowExists(0, ScriptableObject.CreateInstance<DoCreateAudioMixer>(), "NewAudioMixer.mixer", icon, null);
        }

        static private void CreateSpritePolygon(int sides)
        {
            string assetName = "";
            switch (sides)
            {
                case 0:
                    assetName = "Square";
                    break;
                case 3:
                    assetName = "Triangle";
                    break;
                case 4:
                    assetName = "Diamond";
                    break;
                case 6:
                    assetName = "Hexagon";
                    break;
                case 42:
                    // http://hitchhikers.wikia.com/wiki/42
                    assetName = "Everythingon";
                    break;
                case 128:
                    assetName = "Circle";
                    break;
                default:
                    assetName = "Polygon";
                    break;
            }

            var icon = EditorGUIUtility.IconContent("Sprite Icon").image as Texture2D;
            DoCreateSpritePolygon action = ScriptableObject.CreateInstance<DoCreateSpritePolygon>();
            action.sides = sides;
            StartNameEditingIfProjectWindowExists(0, action, assetName + ".png", icon, null);
        }

        internal static Object CreateScriptAssetFromTemplate(string pathName, string resourceFile)
        {
            string fullPath = Path.GetFullPath(pathName);

            string content = File.ReadAllText(resourceFile);

            // #NOTRIM# is a special marker that is used to mark the end of a line where we want to leave whitespace. prevent editors auto-stripping it by accident.
            content = content.Replace("#NOTRIM#", "");

            // macro replacement
            string baseFile = Path.GetFileNameWithoutExtension(pathName);
            content = content.Replace("#NAME#", baseFile);
            string baseFileNoSpaces = baseFile.Replace(" ", "");
            content = content.Replace("#SCRIPTNAME#", baseFileNoSpaces);

            // if the script name begins with an uppercase character we support a lowercase substitution variant
            if (char.IsUpper(baseFileNoSpaces, 0))
            {
                baseFileNoSpaces = char.ToLower(baseFileNoSpaces[0]) + baseFileNoSpaces.Substring(1);
                content = content.Replace("#SCRIPTNAME_LOWER#", baseFileNoSpaces);
            }
            else
            {
                // still allow the variant, but change the first character to upper and prefix with "my"
                baseFileNoSpaces = "my" + char.ToUpper(baseFileNoSpaces[0]) + baseFileNoSpaces.Substring(1);
                content = content.Replace("#SCRIPTNAME_LOWER#", baseFileNoSpaces);
            }

            // utf8-bom encoding was added for case 510374 in 2012. i think this was the wrong solution. BOM's are
            // problematic for diff tools, naive readers and writers (of which we have many!), and generally not
            // something most people think about. you wouldn't believe how many unity source files have BOM's embedded
            // in the middle of them for no reason. copy paste problem? bad tool? unity should instead have been fixed
            // to read all files that have no BOM as utf8 by default, and then we just strip them all, always, from
            // files we control. perhaps we'll do this one day and this next line can be removed. -scobi
            var encoding = new System.Text.UTF8Encoding(/*encoderShouldEmitUTF8Identifier:*/ true);

            File.WriteAllText(fullPath, content, encoding);

            // Import the asset
            AssetDatabase.ImportAsset(pathName);

            return AssetDatabase.LoadAssetAtPath(pathName, typeof(Object));
        }

        public static void StartNameEditingIfProjectWindowExists(int instanceID, EndNameEditAction endAction, string pathName, Texture2D icon, string resourceFile)
        {
            ProjectBrowser pb = GetProjectBrowserIfExists();
            if (pb)
            {
                pb.Focus();
                pb.BeginPreimportedNameEditing(instanceID, endAction, pathName, icon, resourceFile);
                pb.Repaint();
            }
            else
            {
                if (!pathName.StartsWith("assets/", System.StringComparison.CurrentCultureIgnoreCase))
                    pathName = "Assets/" + pathName;
                EndNameEditAction(endAction, instanceID, pathName, resourceFile);
                Selection.activeObject = EditorUtility.InstanceIDToObject(instanceID);
            }
        }

        static ProjectBrowser GetProjectBrowserIfExists()
        {
            return ProjectBrowser.s_LastInteractedProjectBrowser;
        }

        internal static void FrameObjectInProjectWindow(int instanceID)
        {
            ProjectBrowser pb = GetProjectBrowserIfExists();
            if (pb)
            {
                pb.FrameObject(instanceID, false);
            }
        }

        // InstanceIDs larger than this is considered a favorite by the projectwindows
        internal static int k_FavoritesStartInstanceID = 1000000000;
        internal static string k_DraggingFavoriteGenericData = "DraggingFavorite";
        internal static string k_IsFolderGenericData = "IsFolder";

        internal static bool IsFavoritesItem(int instanceID)
        {
            return instanceID >= k_FavoritesStartInstanceID;
        }

        internal static void StartDrag(int draggedInstanceID, List<int> selectedInstanceIDs)
        {
            DragAndDrop.PrepareStartDrag();

            string title = "";
            if (IsFavoritesItem(draggedInstanceID))
            {
                DragAndDrop.SetGenericData(k_DraggingFavoriteGenericData, draggedInstanceID);
                DragAndDrop.objectReferences = new UnityEngine.Object[] {}; // this IS required for dragging to work
            }
            else
            {
                // Normal assets dragging
                bool isFolder = IsFolder(draggedInstanceID);
                DragAndDrop.objectReferences = GetDragAndDropObjects(draggedInstanceID, selectedInstanceIDs);
                DragAndDrop.SetGenericData(k_IsFolderGenericData, isFolder ? "isFolder" : "");
                string[] paths = GetDragAndDropPaths(draggedInstanceID, selectedInstanceIDs);
                if (paths.Length > 0)
                    DragAndDrop.paths = paths;

                if (DragAndDrop.objectReferences.Length > 1)
                    title = "<Multiple>";
                else
                    title = ObjectNames.GetDragAndDropTitle(InternalEditorUtility.GetObjectFromInstanceID(draggedInstanceID));
            }

            DragAndDrop.StartDrag(title);
        }

        internal static Object[] GetDragAndDropObjects(int draggedInstanceID, List<int> selectedInstanceIDs)
        {
            List<Object> outList = new List<Object>(selectedInstanceIDs.Count);
            if (selectedInstanceIDs.Contains(draggedInstanceID))
            {
                for (int i = 0; i < selectedInstanceIDs.Count; ++i)
                {
                    Object obj = InternalEditorUtility.GetObjectFromInstanceID(selectedInstanceIDs[i]);
                    if (obj != null)
                        outList.Add(obj);
                }
            }
            else
            {
                Object obj = InternalEditorUtility.GetObjectFromInstanceID(draggedInstanceID);
                if (obj != null)
                    outList.Add(obj);
            }
            return outList.ToArray();
        }

        internal static string[] GetDragAndDropPaths(int draggedInstanceID, List<int> selectedInstanceIDs)
        {
            // Assets
            List<string> paths = new List<string>();
            foreach (int instanceID in selectedInstanceIDs)
            {
                if (AssetDatabase.IsMainAsset(instanceID))
                {
                    string path = AssetDatabase.GetAssetPath(instanceID);
                    paths.Add(path);
                }
            }

            string dragPath = AssetDatabase.GetAssetPath(draggedInstanceID);
            if (!string.IsNullOrEmpty(dragPath))
            {
                if (paths.Contains(dragPath))
                {
                    return paths.ToArray();
                }
                else
                {
                    return new[] { dragPath };
                }
            }
            return new string[0];
        }

        // Returns instanceID of folders (and main asset if input is a subasset) up until and including the Assets folder
        public static int[] GetAncestors(int instanceID)
        {
            List<int> ancestors = new List<int>();

            // Ensure we add the main asset as ancestor if input is a subasset
            int mainAssetInstanceID = AssetDatabase.GetMainAssetInstanceID(AssetDatabase.GetAssetPath(instanceID));
            bool isSubAsset = mainAssetInstanceID != instanceID;
            if (isSubAsset)
                ancestors.Add(mainAssetInstanceID);

            // Find ancestors of main aset
            string currentFolderPath = GetContainingFolder(AssetDatabase.GetAssetPath(mainAssetInstanceID));
            while (!string.IsNullOrEmpty(currentFolderPath))
            {
                int currentInstanceID = AssetDatabase.GetMainAssetInstanceID(currentFolderPath);
                ancestors.Add(currentInstanceID);
                currentFolderPath = GetContainingFolder(AssetDatabase.GetAssetPath(currentInstanceID));
            }

            return ancestors.ToArray();
        }

        public static bool IsFolder(int instanceID)
        {
            return AssetDatabase.IsValidFolder(AssetDatabase.GetAssetPath(instanceID));
        }

        // Returns containing folder if possible otherwise null.
        // Trims any trailing forward slashes
        public static string GetContainingFolder(string path)
        {
            if (string.IsNullOrEmpty(path))
                return null;

            path = path.Trim('/');
            int pos = path.LastIndexOf("/", StringComparison.Ordinal);
            if (pos != -1)
            {
                return path.Substring(0, pos);
            }

            // Could not determine containing folder
            return null;
        }

        // Input the following list:
        //  assets/flesh/big
        //  assets/icons/duke
        //  assets/icons/duke/snake
        //  assets/icons/duke/zoo
        //
        // ... And the returned list becomes:
        //  assets/flesh/big
        //  assets/icons/duke

        // Returned paths are trimmed for ending slashes
        public static string[] GetBaseFolders(string[] folders)
        {
            if (folders.Length <= 1)
                return folders;

            List<string> result = new List<string>();
            List<string> sortedFolders = new List<string>(folders);

            // Remove forward slashes before sorting otherwise will "Assets 1/" come before "Assets/"
            // which we do not want in the find base folders section below
            for (int i = 0; i < sortedFolders.Count; ++i)
                sortedFolders[i] = sortedFolders[i].Trim('/');

            sortedFolders.Sort();

            // Ensure folder paths are ending with '/' so e.g: "assets/" is not found in "assets 1/".
            // If we did not end with '/' then "assets" could be found in "assets 1"
            // which is not what we want when finding base folders
            for (int i = 0; i < sortedFolders.Count; ++i)
                if (!sortedFolders[i].EndsWith("/"))
                    sortedFolders[i] = sortedFolders[i] + "/";

            // Find base folders
            // We assume sortedFolders is sorted with less first. E.g: {assets/, assets/icons/}
            string curPath = sortedFolders[0];
            result.Add(curPath);
            for (int i = 1; i < sortedFolders.Count; ++i)
            {
                // Ensure path matches from start of curPath (to ensure "assets/monkey" and "npc/assets/monkey" both are returned as base folders)
                bool startOfPathMatches = sortedFolders[i].IndexOf(curPath, StringComparison.Ordinal) == 0;
                if (!startOfPathMatches)
                {
                    // Add tested path if not part of current path and use tested path as new base
                    result.Add(sortedFolders[i]);
                    curPath = sortedFolders[i];
                }
            }

            // Remove forward slashes again (added above)
            for (int i = 0; i < result.Count; ++i)
                result[i] = result[i].Trim('/');

            return result.ToArray();
        }

        static internal void DuplicateSelectedAssets()
        {
            AssetDatabase.Refresh();

            // If we are duplicating an animation clip which comes from a imported model. Instead of duplicating the fbx file, we duplicate the animation clip.
            // Thus the user can edit and add for example animation events.
            Object[] selectedAnimations = Selection.objects;
            bool duplicateClip = true;
            foreach (Object asset in selectedAnimations)
            {
                AnimationClip clip = asset as AnimationClip;
                if (clip == null || !AssetDatabase.Contains(clip))
                    duplicateClip = false;
            }

            ArrayList copiedPaths = new ArrayList();
            bool failed = false;

            if (duplicateClip)
            {
                foreach (Object asset in selectedAnimations)
                {
                    AnimationClip sourceClip = asset as AnimationClip;
                    if (sourceClip != null)
                    {
                        string path = AssetDatabase.GetAssetPath(asset);
                        path = Path.Combine(Path.GetDirectoryName(path), sourceClip.name) + ".anim";
                        string newPath = AssetDatabase.GenerateUniqueAssetPath(path);

                        AnimationClip newClip = new AnimationClip();
                        EditorUtility.CopySerialized(sourceClip, newClip);
                        AssetDatabase.CreateAsset(newClip, newPath);
                        copiedPaths.Add(newPath);
                    }
                }
            }
            else
            {
                Object[] selectedAssets = Selection.GetFiltered(typeof(Object), SelectionMode.Assets);

                foreach (Object asset in selectedAssets)
                {
                    string assetPath = AssetDatabase.GetAssetPath(asset);
                    string newPath = AssetDatabase.GenerateUniqueAssetPath(assetPath);

                    // Copy
                    if (newPath.Length != 0)
                        failed |= !AssetDatabase.CopyAsset(assetPath, newPath);
                    else
                        failed |= true;

                    if (!failed)
                    {
                        copiedPaths.Add(newPath);
                    }
                }
            }

            //              if (failed)
            //                  UnityBeep ();

            AssetDatabase.Refresh();

            Object[] copiedAssets = new Object[copiedPaths.Count];
            for (int i = 0; i < copiedPaths.Count; i++)
            {
                copiedAssets[i] = AssetDatabase.LoadMainAssetAtPath(copiedPaths[i] as string);
            }

            Selection.objects = copiedAssets;
        }
    }
}