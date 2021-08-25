using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.Animations;
using System;
using System.Linq;
using System.Collections.Generic;
using ExpressionsMenu = VRC.SDK3.Avatars.ScriptableObjects.VRCExpressionsMenu;
using ExpressionControl = VRC.SDK3.Avatars.ScriptableObjects.VRCExpressionsMenu.Control;
using ExpressionParameters = VRC.SDK3.Avatars.ScriptableObjects.VRCExpressionParameters;

/*
 * ・テクスチャを切り替えるExpressionMenuを生成する
 * ・ExpressionMenuに使うアニメーションを生成する
 * ・ExpressionMenuのためにExpressionParametersを変更する
 * ・テクスチャ切り替えの負荷を減らすためAtlasTextureを生成する
 * ・Quad専用 ほかには後で対応するかも
 * 
 * ・AnimatorController上書き
 * ・ExpressionMenu上書き出力
 * ・ExpressionParameters追加のみ
 * ・Texture上書き出力(AssetDataBaseなので2回目以降は自動)
 * ・Animation上書き出力(AssetDataBaseなので2回目以降は自動)
 */


[Serializable]
public struct SubMenu
{
    public List<TextureData> Textures;
    public string Name;
    public Texture2D Icon;
}

class TSEditorWindow : EditorWindow
{
    const string localPath = "Assets/Atodeyaru/TextureSwitchEditor/Resources";
    const string editorPath = "Assets/Atodeyaru/TextureSwitchEditor";

    //TextureSetting setting = null;
    GameObject targetObject = null;
    AnimatorController targetAnimatorController = null;
    ExpressionsMenu targetExpressionsMenu = null;
    ExpressionParameters targetExpressionParameters = null;
    TextureSetting TextureSetting = null;
    Vector2 scrollPosL = Vector2.zero;
    Vector2 scrollPosR = Vector2.zero;
    List<SubMenu> textureDataList = new List<SubMenu>();
    int atlasSizePopup = 1024, atlasSize;
    int targetTexturePopup = 0;
    int selectedSub = -1;
    int expressionDefault = 0;
    int basePixelCount = 128;
    bool expressionSaved = false;
    bool isSettingExport = true;
    bool advancedSetting = false;
    bool importSetting = false;
    string ExpressionParameterName = "parameter";
    string ExpressionsMenuName = "menu";
    string AnimatorControllerLayerName = "layer";
    Texture2D ExpressionMenuIcon = null;

    [MenuItem("Atodeyaru/TextureSwitchEditor")]
    static void Init()
    {
        TSEditorWindow window = GetWindow<TSEditorWindow>("TextureSwitchEditor");
        window.position = new Rect(0, 0, 800, 800);
        window.Show();
    }

    private void OnGUI()
    {
        GUILayout.BeginArea(new Rect(20, 20, position.size.x - 40, position.size.y - 40));

        targetObject = (GameObject)EditorGUILayout.ObjectField("Object", targetObject, typeof(GameObject), true);
        targetAnimatorController = (AnimatorController)EditorGUILayout.ObjectField("AnimatorController", targetAnimatorController, typeof(AnimatorController), true);
        targetExpressionParameters = (ExpressionParameters)EditorGUILayout.ObjectField("ExpressionsParameters", targetExpressionParameters, typeof(ExpressionParameters), true);
        targetExpressionsMenu = (ExpressionsMenu)EditorGUILayout.ObjectField("ExpressionsMenu", targetExpressionsMenu, typeof(ExpressionsMenu), true);

        if (targetObject == null) EditorGUILayout.HelpBox("対象となるObjectを指定してください", MessageType.Info);
        if (targetObject != null) if (targetObject.GetComponent<MeshRenderer>() == null) EditorGUILayout.HelpBox("MeshRendererのあるObjectを指定してください", MessageType.Warning);
        if (targetAnimatorController == null) EditorGUILayout.HelpBox("対象となるAnimationContorollerを指定してください", MessageType.Info);
        if (targetExpressionParameters == null) EditorGUILayout.HelpBox("対象となるExpressionParametersを指定してください", MessageType.Info);
        if (targetExpressionParameters != null) if (targetExpressionParameters.CalcTotalCost() > ExpressionParameters.MAX_PARAMETER_COST) EditorGUILayout.HelpBox("ExpressionParametersのメモリ使用量を減らしてください", MessageType.Warning);
        if (targetExpressionsMenu == null) EditorGUILayout.HelpBox("対象となるExpressionMenuを指定してください", MessageType.Info);
        if (targetExpressionsMenu != null) if (targetExpressionsMenu.controls.Count >= ExpressionsMenu.MAX_CONTROLS) EditorGUILayout.HelpBox("ExpressionMenuのControl数を減らしてください", MessageType.Warning);

        GUILayout.Space(20);

        atlasSizePopup = EditorGUILayout.IntPopup("Atlas Size", atlasSizePopup, new string[] { "1024", "2048", "4096", "8192", "Custom" }, new int[] { 1024, 2048, 4096, 8192, -1 });
        if (atlasSizePopup != -1)
        {
            atlasSize = atlasSizePopup;
        }
        else
        {
            atlasSize = EditorGUILayout.IntField("Custom Atlas Size", atlasSize);
        }

        var targetTextureNames = new string[0];
        if (targetObject != null) if (targetObject.GetComponent<MeshRenderer>() != null) targetTextureNames = targetObject.GetComponent<MeshRenderer>().sharedMaterial.GetTexturePropertyNames();
        targetTexturePopup = EditorGUILayout.Popup("Shader Property", targetTexturePopup, targetTextureNames);

        advancedSetting = EditorGUILayout.Foldout(advancedSetting, "Advanced Setting");
        if (advancedSetting)
        {
            AnimatorControllerLayerName = EditorGUILayout.TextField("Layer Name", AnimatorControllerLayerName);
            ExpressionsMenuName = EditorGUILayout.TextField("Expression Menu Name", ExpressionsMenuName);
            ExpressionMenuIcon = (Texture2D)EditorGUILayout.ObjectField("Expression Menu Icon", ExpressionMenuIcon, typeof(Texture2D), false);
            ExpressionParameterName = EditorGUILayout.TextField("Expression Parameter Name", ExpressionParameterName);
            expressionDefault = EditorGUILayout.IntField("Default Expression Value", expressionDefault);
            expressionSaved = EditorGUILayout.Toggle("Expression Saved", expressionSaved);
            basePixelCount = EditorGUILayout.IntField("Base Pixel Count", basePixelCount);
            isSettingExport = EditorGUILayout.Toggle("Export Setting", isSettingExport);
        }

        importSetting = EditorGUILayout.Foldout(importSetting, "Import Setting");
        if (importSetting)
        {
            TextureSetting = (TextureSetting)EditorGUILayout.ObjectField("Setting Data", TextureSetting, typeof(TextureSetting), false);

            EditorGUI.BeginDisabledGroup(TextureSetting == null);
            {
                if (GUILayout.Button("Import"))
                {
                    var tf = SceneManager.GetSceneByName(TextureSetting.sceneName).GetRootGameObjects().First(v => v.name == TextureSetting.rootObjectName).transform;
                    foreach (var item in TextureSetting.gameObjectPath.Split('/')) tf = tf.Find(item);
                    targetObject = tf.gameObject;
                    targetAnimatorController = TextureSetting.animatorController;
                    targetExpressionsMenu = TextureSetting.expressionsMenu;
                    targetExpressionParameters = TextureSetting.expressionPatameters;
                    textureDataList = new List<SubMenu>(TextureSetting.textureDataList);
                    atlasSizePopup = TextureSetting.atlasSizePopup;
                    atlasSize = TextureSetting.atlasSize;
                    targetTexturePopup = TextureSetting.targetTexture;
                    expressionDefault = TextureSetting.expressionDefault;
                    basePixelCount = TextureSetting.basePixelCount;
                    expressionSaved = TextureSetting.expressionSaved;
                    ExpressionParameterName = TextureSetting.ExpressionParameterName;
                    ExpressionsMenuName = TextureSetting.ExpressionsMenuName;
                    AnimatorControllerLayerName = TextureSetting.AnimatorControllerLayerName;
                    ExpressionMenuIcon = TextureSetting.ExpressionMenuIcon;
                    Repaint();
                }
            }
            EditorGUI.EndDisabledGroup();
        }

        GUILayout.Space(20);

        if (textureDataList.Count < selectedSub + 1) selectedSub = -1;

        EditorGUILayout.BeginHorizontal();
        {
            //Menu
            EditorGUILayout.BeginVertical(GUILayout.MaxWidth(position.width / 2));
            {
                EditorGUI.BeginDisabledGroup(textureDataList.Count > 7);
                {
                    if (GUILayout.Button("Add SubMenu"))
                    {
                        textureDataList.Add(new SubMenu { Name = "New SubMenu", Textures = new List<TextureData>() });
                    }
                }
                EditorGUI.EndDisabledGroup();

                scrollPosL = GUILayout.BeginScrollView(scrollPosL, GUI.skin.box);
                {
                    for (int i = 0; i < textureDataList.Count; i++)
                    {
                        var rect = EditorGUILayout.BeginVertical(GUI.skin.box);
                        {
                            EditorGUILayout.BeginHorizontal();
                            {
                                if (GUILayout.Button("Edit"))
                                {
                                    selectedSub = i;
                                }

                                if (GUILayout.Button("Up"))
                                {
                                    if (i > 0)
                                    {
                                        SwapSubs(i - 1, i);
                                        selectedSub = -1;
                                        Repaint();
                                    }
                                }

                                if (GUILayout.Button("Down"))
                                {
                                    if (i < textureDataList.Count - 1)
                                    {
                                        SwapSubs(i, i + 1);
                                        selectedSub = -1;
                                        Repaint();
                                    }
                                }

                                void SwapSubs(int indexA, int indexB)
                                {
                                    var itemA = textureDataList[indexA];
                                    var itemB = textureDataList[indexB];
                                    textureDataList[indexA] = itemB;
                                    textureDataList[indexB] = itemA;
                                }

                                if (GUILayout.Button("Delete"))
                                {
                                    textureDataList.RemoveAt(i);
                                    selectedSub = -1;
                                    if (textureDataList.Count < 1) break;
                                }
                            }
                            EditorGUILayout.EndHorizontal();

                            var str = textureDataList[i];
                            str.Icon = (Texture2D)EditorGUILayout.ObjectField("SubMenu Icon", textureDataList[i].Icon, typeof(Texture2D), false);
                            str.Name = EditorGUILayout.TextField("SubMenu Name", textureDataList[i].Name);
                            textureDataList[i] = str;
                        }
                        EditorGUILayout.EndVertical();

                        //Select
                        if (Event.current.type == EventType.MouseDown)
                        {
                            if (rect.Contains(Event.current.mousePosition))
                            {
                                selectedSub = i;
                                Event.current.Use();
                            }
                        }
                    }
                }
                GUILayout.EndScrollView();
            }
            EditorGUILayout.EndVertical();

            GUILayout.Space(20);

            //SubMenu
            EditorGUILayout.BeginVertical(GUILayout.MaxWidth(position.width / 2));
            {
                bool isListOver = true;

                if (selectedSub > -1) if (textureDataList[selectedSub].Textures.Count < 8) isListOver = false;

                EditorGUI.BeginDisabledGroup(isListOver);
                {
                    if (GUILayout.Button("Add Texture"))
                    {
                        textureDataList[selectedSub].Textures.Add(new TextureData { name = "New Texture" });
                    }
                }
                EditorGUI.EndDisabledGroup();

                scrollPosR = GUILayout.BeginScrollView(scrollPosR, GUI.skin.box);
                {
                    if (selectedSub > -1 && textureDataList[selectedSub].Textures != null)
                    {
                        for (int i = 0; i < textureDataList[selectedSub].Textures.Count; i++)
                        {
                            EditorGUILayout.BeginVertical(GUI.skin.box);
                            {
                                bool delete = false;
                                EditorGUILayout.BeginHorizontal();
                                {
                                    if (GUILayout.Button("Up"))
                                    {
                                        if (i > 0)
                                        {
                                            SwapSubs(i - 1, i);
                                            selectedSub = -1;
                                            Repaint();
                                        }
                                    }

                                    if (GUILayout.Button("Down"))
                                    {
                                        if (i < textureDataList.Count - 1)
                                        {
                                            SwapSubs(i, i + 1);
                                            selectedSub = -1;
                                            Repaint();
                                        }
                                    }

                                    void SwapSubs(int indexA, int indexB)
                                    {
                                        var itemA = textureDataList[selectedSub].Textures[indexA];
                                        var itemB = textureDataList[selectedSub].Textures[indexB];
                                        textureDataList[selectedSub].Textures[indexA] = itemB;
                                        textureDataList[selectedSub].Textures[indexB] = itemA;
                                    }

                                    if (GUILayout.Button("Delete"))
                                    {
                                        delete = true;
                                    }
                                }
                                EditorGUILayout.EndHorizontal();

                                var str = textureDataList[selectedSub].Textures[i];
                                str.texture = (Texture2D)EditorGUILayout.ObjectField("Texture", textureDataList[selectedSub].Textures[i].texture, typeof(Texture2D), false);
                                str.isCustomIcon = EditorGUILayout.Toggle("Enable Custom Icon", textureDataList[selectedSub].Textures[i].isCustomIcon);
                                if (str.isCustomIcon) str.customIcon = (Texture2D)EditorGUILayout.ObjectField("Custom Icon", textureDataList[selectedSub].Textures[i].customIcon, typeof(Texture2D), false);
                                str.name = EditorGUILayout.TextField("Name", textureDataList[selectedSub].Textures[i].name);
                                str.resizeMode = (TextureData.ResizeMode)EditorGUILayout.EnumPopup("Resize Mode", textureDataList[selectedSub].Textures[i].resizeMode);
                                switch (str.resizeMode)
                                {
                                    case TextureData.ResizeMode.Fixed:
                                    case TextureData.ResizeMode.Proportional:
                                        break;
                                    case TextureData.ResizeMode.Absolute:
                                    case TextureData.ResizeMode.Relative:
                                        str.resizeValue = EditorGUILayout.Vector2Field("Resize Value", textureDataList[selectedSub].Textures[i].resizeValue);
                                        break;
                                    default:
                                        break;
                                }
                                textureDataList[selectedSub].Textures[i] = str;

                                if (delete) textureDataList[selectedSub].Textures.RemoveAt(i);
                            }
                            EditorGUILayout.EndVertical();
                        }
                    }
                }
                GUILayout.EndScrollView();
            }
            EditorGUILayout.EndVertical();
        }
        EditorGUILayout.EndHorizontal();

        GUILayout.Space(20);
        
        EditorGUI.BeginDisabledGroup(
            textureDataList.Count == 0 ||
            textureDataList.Any(l => l.Textures.Count == 0) ||
            textureDataList.Any(l => l.Textures.Any(v => v.texture == null)) ||
            targetObject == null || targetAnimatorController == null ||
            targetExpressionsMenu == null ||
            targetExpressionParameters == null);
        {
            if (GUILayout.Button("Export"))
            {
                if (!AssetDatabase.IsValidFolder(localPath)) AssetDatabase.CreateFolder(editorPath, "Resources");

                //Parameter
                var parameter = new ExpressionParameters.Parameter();
                parameter.valueType = ExpressionParameters.ValueType.Int;
                parameter.name = ExpressionParameterName;
                parameter.saved = expressionSaved;
                parameter.defaultValue = expressionDefault;

                if (!targetExpressionParameters.parameters.Any(v => v.name == ExpressionParameterName))
                {
                    Array.Resize(ref targetExpressionParameters.parameters, targetExpressionParameters.parameters.Length + 1);
                    targetExpressionParameters.parameters[targetExpressionParameters.parameters.Length - 1] = parameter;
                }
                else
                {
                    var targetParameter = targetExpressionParameters.parameters.Select((value, index) => new { value, index }).First(v => v.value.name == ExpressionParameterName);
                    targetExpressionParameters.parameters[targetParameter.index] = parameter;
                }

                if (!targetAnimatorController.parameters.Any(v => v.name == ExpressionParameterName)) targetAnimatorController.AddParameter(ExpressionParameterName, AnimatorControllerParameterType.Int);

                //AnimationControllerLayer
                if (targetAnimatorController.layers.Any(v => v.name == AnimatorControllerLayerName))
                {
                    var layers = targetAnimatorController.layers.Select((value, index) => new { value, index }).First(v => v.value.name == AnimatorControllerLayerName);
                    targetAnimatorController.RemoveLayer(layers.index);
                }

                var layer = new AnimatorControllerLayer();
                layer.name = AnimatorControllerLayerName;
                layer.defaultWeight = 1.0f;
                layer.stateMachine = new AnimatorStateMachine();

                //StateMachine
                var stateMachine = layer.stateMachine;
                stateMachine.name = AnimatorControllerLayerName;
                stateMachine.hideFlags = HideFlags.HideInHierarchy;
                stateMachine.anyStatePosition = new Vector3(-480.0f, 0.0f, 0.0f);
                stateMachine.entryPosition = new Vector3(-480.0f, 60.0f, 0.0f);
                stateMachine.exitPosition = new Vector3(-480.0f, 120.0f, 0.0f);

                AssetDatabase.AddObjectToAsset(stateMachine, AssetDatabase.GetAssetPath(targetAnimatorController));

                var baseState = stateMachine.AddState("None", new Vector3(-240.0f, 0.0f, 0.0f));

                if (!AssetDatabase.IsValidFolder(localPath + "/" + targetObject.ToString())) AssetDatabase.CreateFolder(localPath, targetObject.ToString());

                //CreateAtlas
                foreach (var list in textureDataList) foreach (var item in list.Textures) item.StoreAndSetReadable();
                var textureArray = textureDataList.SelectMany(l => l.Textures.Select(v => v.texture)).ToArray();

                var atlasTexture = new Texture2D(atlasSize, atlasSize);
                var atlasRects = atlasTexture.PackTextures(textureArray, 2, atlasSize);
                AssetDatabase.CreateAsset(atlasTexture, localPath + "/" + targetObject.ToString() + "/" + "atlas.asset");

                foreach (var list in textureDataList) foreach (var item in list.Textures) item.ResetReadable();

                targetObject.GetComponent<MeshRenderer>().sharedMaterial.SetTexture(targetTextureNames[targetTexturePopup], atlasTexture);

                var mainMenu = new ExpressionsMenu();

                int i = 0;
                foreach (var list in textureDataList.Select((value, index) => new { value, index }))
                {
                    var subMenu = new ExpressionsMenu();

                    foreach (var item in list.value.Textures.Select((value, index) => new { value, index }))
                    {
                        //AnimationClip
                        var animationClip = new AnimationClip();
                        var animationCurveX = new AnimationCurve(new Keyframe(0.0f, atlasRects[i].width));
                        var animationCurveY = new AnimationCurve(new Keyframe(0.0f, atlasRects[i].height));
                        var animationCurveZ = new AnimationCurve(new Keyframe(0.0f, atlasRects[i].xMin));
                        var animationCurveW = new AnimationCurve(new Keyframe(0.0f, atlasRects[i].yMin));

                        float scaleX = 1.0f;
                        float scaleY = 1.0f;
                        float scaleZ = 1.0f;

                        switch (item.value.resizeMode)
                        {
                            case TextureData.ResizeMode.Fixed:
                                break;
                            case TextureData.ResizeMode.Proportional:
                                scaleX = item.value.texture.width / basePixelCount;
                                scaleY = item.value.texture.height / basePixelCount;
                                break;
                            case TextureData.ResizeMode.Absolute:
                                scaleX = item.value.resizeValue.x;
                                scaleY = item.value.resizeValue.y;
                                break;
                            case TextureData.ResizeMode.Relative:
                                scaleX = item.value.resizeValue.x / basePixelCount;
                                scaleY = item.value.resizeValue.y / basePixelCount;
                                break;
                            default:
                                break;
                        }

                        var animationCurveScaleX = new AnimationCurve(new Keyframe(0.0f, scaleX));
                        var animationCurveScaleY = new AnimationCurve(new Keyframe(0.0f, scaleY));
                        var animationCurveScaleZ = new AnimationCurve(new Keyframe(0.0f, scaleZ));

                        animationClip.SetCurve(GetObjectPath(targetObject), typeof(MeshRenderer), "material." + targetTextureNames[targetTexturePopup] + "_ST.x", animationCurveX);
                        animationClip.SetCurve(GetObjectPath(targetObject), typeof(MeshRenderer), "material." + targetTextureNames[targetTexturePopup] + "_ST.y", animationCurveY);
                        animationClip.SetCurve(GetObjectPath(targetObject), typeof(MeshRenderer), "material." + targetTextureNames[targetTexturePopup] + "_ST.z", animationCurveZ);
                        animationClip.SetCurve(GetObjectPath(targetObject), typeof(MeshRenderer), "material." + targetTextureNames[targetTexturePopup] + "_ST.w", animationCurveW);

                        animationClip.SetCurve(GetObjectPath(targetObject), typeof(Transform), "localScale.x", animationCurveScaleX);
                        animationClip.SetCurve(GetObjectPath(targetObject), typeof(Transform), "localScale.y", animationCurveScaleY);
                        animationClip.SetCurve(GetObjectPath(targetObject), typeof(Transform), "localScale.z", animationCurveScaleZ);

                        AssetDatabase.CreateAsset(animationClip, localPath + "/" + targetObject.ToString() + "/" + list.index.ToString() + item.index.ToString() + ".anim");

                        //AnimationController
                        var state = stateMachine.AddState(item.value.name, new Vector3(list.index * 240.0f, item.index * 60.0f, 0.0f));
                        //state.name = item.value.Name;
                        state.motion = animationClip;
                        var transition = state.AddTransition(baseState);
                        transition.hasFixedDuration = false;
                        transition.duration = 0.0f;
                        transition.AddCondition(AnimatorConditionMode.NotEqual, list.index * 8 + item.index, ExpressionParameterName);

                        var baseTransition = baseState.AddTransition(state);
                        baseTransition.hasFixedDuration = false;
                        baseTransition.duration = 0.0f;
                        baseTransition.AddCondition(AnimatorConditionMode.Equals, list.index * 8 + item.index, ExpressionParameterName);


                        //SubExpressionsMenu
                        var subControl = new ExpressionControl();
                        subControl.name = item.value.name;
                        subControl.icon = item.value.isCustomIcon ? item.value.customIcon : item.value.texture;
                        subControl.type = ExpressionControl.ControlType.Toggle;
                        var name = new ExpressionControl.Parameter();
                        name.name = ExpressionParameterName;
                        subControl.parameter = name;
                        subControl.value = list.index * 8 + item.index;
                        subMenu.controls.Add(subControl);

                        i++;
                    }

                    //MainExpressionsMenu
                    var mainControl = new ExpressionControl();
                    mainControl.name = list.value.Name;
                    mainControl.icon = list.value.Icon;
                    mainControl.type = ExpressionControl.ControlType.SubMenu;
                    mainControl.subMenu = subMenu;
                    mainMenu.controls.Add(mainControl);

                    AssetDatabase.CreateAsset(subMenu, localPath + "/" + targetObject.ToString() + "/" + list.value.Name + ".asset");
                }
                AssetDatabase.CreateAsset(mainMenu, localPath + "/" + targetObject.ToString() + "/" + "MainMenu.asset");

                var targetControl = new ExpressionControl();
                targetControl.name = ExpressionsMenuName;
                if (!targetExpressionsMenu.controls.Any(v => v.name == ExpressionsMenuName)) targetExpressionsMenu.controls.Add(targetControl);
                targetControl = targetExpressionsMenu.controls.First(v => v.name == ExpressionsMenuName);
                targetControl.type = ExpressionControl.ControlType.SubMenu;
                targetControl.subMenu = mainMenu;

                targetAnimatorController.AddLayer(layer);

                //SettingData
                if (isSettingExport)
                {
                    var textureSetting = (TextureSetting)CreateInstance("TextureSetting");
                    textureSetting.sceneName = SceneManager.GetActiveScene().name;
                    textureSetting.rootObjectName = targetObject.transform.root.gameObject.name;
                    textureSetting.gameObjectPath = GetObjectPath(targetObject);
                    textureSetting.animatorController = targetAnimatorController;
                    textureSetting.expressionsMenu = targetExpressionsMenu;
                    textureSetting.expressionPatameters = targetExpressionParameters;
                    textureSetting.textureDataList = new List<SubMenu>(textureDataList);
                    textureSetting.atlasSizePopup = atlasSizePopup;
                    textureSetting.atlasSize = atlasSize;
                    textureSetting.targetTexture = targetTexturePopup;
                    textureSetting.expressionDefault = expressionDefault;
                    textureSetting.basePixelCount = basePixelCount;
                    textureSetting.expressionSaved = expressionSaved;
                    textureSetting.ExpressionParameterName = ExpressionParameterName;
                    textureSetting.ExpressionsMenuName = ExpressionsMenuName;
                    textureSetting.AnimatorControllerLayerName = AnimatorControllerLayerName;
                    textureSetting.ExpressionMenuIcon = ExpressionMenuIcon;
                    AssetDatabase.CreateAsset(textureSetting, localPath + "/" + targetObject.ToString() + "/Setting.asset");
                }
            }
        }
        EditorGUI.EndDisabledGroup();

        GUILayout.EndArea();
    }

    /// <summary>
    /// Returns the path to top of the hierarchy.
    /// </summary>
    /// <param name="obj"></param>
    /// <returns>
    /// The path to top of the hierarchy.
    /// Does not include the root object.
    /// </returns>
    public string GetObjectPath(GameObject obj)
    {
        Transform tf = obj.transform.parent;
        string path = obj.name;
        while (tf.parent != null)
        {
            path = tf.name + "/" + path;
            tf = tf.parent;
        }
        return path;
    }
}