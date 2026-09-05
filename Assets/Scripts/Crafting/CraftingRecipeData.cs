using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class CraftingRecipe
{
    public string makerId;
    public List<string> ingredients;
    public string resultSprite;
}

[System.Serializable]
public class CraftingRecipeList
{
    public List<CraftingRecipe> recipes;
}
