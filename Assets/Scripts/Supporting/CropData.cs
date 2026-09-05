using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]

public class CropStage
{
    public Sprite sprite;
    public float timeToNextStage;
}


[CreateAssetMenu(menuName = "Crop/Crop Data")]
public class CropData : ScriptableObject
{
    public string cropName;       
    public string harvestItemName;  
    public List<CropStage> stages;

    public Sprite outlineSprite;

    [Header("아웃라인 위치")]
    public Vector3 outlineOffset;

    // 나무 전용 플래그/옵션
    public bool isTree = false;      
    public int harvestResetStage = 1;   
    public int minLevelToInteract = 7; 
}