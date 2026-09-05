using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using System.Text;

public class CraftingRecipeManager : MonoBehaviour
{
    public static CraftingRecipeManager Instance;

    private List<CraftingRecipe> allRecipes;

    private static string RecipeKey(CraftingRecipe r) => (r.resultSprite ?? "").Trim();

    void Awake()
    {
        Instance = this;

        TextAsset json = Resources.Load<TextAsset>("Data/CraftingRecipe");
        if (json == null)
        {
            Debug.LogError("레시피 JSON 파일을 찾을 수 없습니다");
            return;
        }

        CraftingRecipeList wrapper = JsonUtility.FromJson<CraftingRecipeList>(json.text);
        if (wrapper == null || wrapper.recipes == null || wrapper.recipes.Count == 0)
        {
            Debug.LogError("레시피 파싱 실패 또는 비어 있음");
            return;
        }

        allRecipes = wrapper.recipes;

    }

    public Sprite GetResultSprite(string makerId, IEnumerable<string> selectedIngredients, out bool isMatched)
    {
        isMatched = false;


        foreach (var recipe in allRecipes)
        {
            // 1) 제작기 잠금 체크
            if (UnlockManager.Instance != null && !UnlockManager.Instance.IsMakerUnlocked(recipe.makerId))
                continue;

            // 2) 레시피 잠금 체크 (키는 resultSprite 이름 사용)
            if (UnlockManager.Instance != null && !UnlockManager.Instance.IsRecipeUnlocked(RecipeKey(recipe)))
                continue;

            bool idMatch = recipe.makerId.Trim().ToLower() == makerId.Trim().ToLower();
            var set1 = new HashSet<string>(recipe.ingredients.Select(i => i.Trim().ToLower()));
            var set2 = new HashSet<string>(selectedIngredients.Select(i => i.Trim().ToLower()));
            bool ingredientsMatch = set1.SetEquals(set2);

            if (idMatch && ingredientsMatch)
            {
                string path = "Sprites/Ingredients/" + recipe.resultSprite;
                Sprite sprite = Resources.Load<Sprite>(path);
                return sprite;
            }
        }

        // 레시피 매칭 실패 시 확률적으로 꽃다발 or 망한떡 반환
        float rand = UnityEngine.Random.value; // 0.0 ~ 1.0
        string failResult = rand < 0.1f ? "FlowerBouquet_finish" : "FailRiceCake_finish";
        string failPath = "Sprites/Ingredients/" + failResult;
        Sprite failSprite = Resources.Load<Sprite>(failPath);
        Debug.LogWarning($"레시피 없음 랜덤 결과: {failResult} (확률={rand})");
        return failSprite;
    }

}
