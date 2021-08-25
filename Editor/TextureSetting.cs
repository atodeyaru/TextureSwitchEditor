using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor.Animations;
using ExpressionsMenu = VRC.SDK3.Avatars.ScriptableObjects.VRCExpressionsMenu;
using ExpressionControl = VRC.SDK3.Avatars.ScriptableObjects.VRCExpressionsMenu.Control;
using ExpressionParameters = VRC.SDK3.Avatars.ScriptableObjects.VRCExpressionParameters;

public class TextureSetting : ScriptableObject
{
    public string sceneName = null;
    public string rootObjectName = null;
    public string gameObjectPath = null;
    public AnimatorController animatorController = null;
    public ExpressionsMenu expressionsMenu = null;
    public ExpressionParameters expressionPatameters = null;
    public List<SubMenu> textureDataList = null;
    public int atlasSizePopup = 1024, atlasSize;
    public int targetTexture = 0;
    public int expressionDefault = 0;
    public int basePixelCount = 0;
    public bool expressionSaved = false;
    public string ExpressionParameterName = "parameter";
    public string ExpressionsMenuName = "menu";
    public string AnimatorControllerLayerName = "layer";
    public Texture2D ExpressionMenuIcon = null;
}
