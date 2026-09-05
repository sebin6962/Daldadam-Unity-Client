using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public static class ProfileRepository
{
    private const string DefaultUsername = "myuser";
    private const string ProfileFileName = "profile_myuser.json";

    private static string ProfilePath =>
        Path.Combine(
            Application.persistentDataPath,
            ProfileFileName
        );

    public static Profile Load()
    {
        if (!File.Exists(ProfilePath))
            return CreateDefaultProfile();

        try
        {
            string json = File.ReadAllText(ProfilePath);
            Profile profile = JsonUtility.FromJson<Profile>(json);

            if (profile == null)
                return CreateDefaultProfile();

            if (string.IsNullOrWhiteSpace(profile.username))
                profile.username = DefaultUsername;

            if (profile.saves == null)
                profile.saves = new List<SaveInfo>();

            return profile;
        }
        catch (Exception exception)
        {
            Debug.LogError(
                "[ProfileRepository] 프로필 불러오기 실패\n" +
                $"경로: {ProfilePath}\n" +
                $"오류: {exception.Message}"
            );

            return CreateDefaultProfile();
        }
    }

    public static bool Save(Profile profile)
    {
        if (profile == null)
        {
            Debug.LogError(
                "[ProfileRepository] 저장할 Profile이 없습니다."
            );

            return false;
        }

        if (string.IsNullOrWhiteSpace(profile.username))
            profile.username = DefaultUsername;

        if (profile.saves == null)
            profile.saves = new List<SaveInfo>();

        try
        {
            string json = JsonUtility.ToJson(profile, true);
            File.WriteAllText(ProfilePath, json);
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogError(
                "[ProfileRepository] 프로필 저장 실패\n" +
                $"경로: {ProfilePath}\n" +
                $"오류: {exception.Message}"
            );

            return false;
        }
    }

    public static bool ContainsSave(string serverName)
    {
        if (string.IsNullOrWhiteSpace(serverName))
            return false;

        Profile profile = Load();

        return profile.saves.Exists(
            save => save != null &&
                    save.serverName == serverName
        );
    }

    public static bool TryAddSave(
        string serverName,
        DateTime createdAt
    )
    {
        if (string.IsNullOrWhiteSpace(serverName))
            return false;

        Profile profile = Load();

        if (profile.saves.Exists(
            save => save != null &&
                    save.serverName == serverName
        ))
        {
            return false;
        }

        string timestamp = createdAt.ToString("s");

        profile.saves.Add(new SaveInfo
        {
            serverName = serverName,
            created = timestamp,
            lastPlayed = timestamp
        });

        return Save(profile);
    }

    public static bool RemoveSave(string serverName)
    {
        if (string.IsNullOrWhiteSpace(serverName))
            return false;

        Profile profile = Load();

        int removedCount = profile.saves.RemoveAll(
            save => save != null &&
                    save.serverName == serverName
        );

        if (removedCount == 0)
            return true;

        return Save(profile);
    }

    public static List<SaveInfo> LoadExistingSaves()
    {
        Profile profile = Load();
        List<SaveInfo> existingSaves = new List<SaveInfo>();

        foreach (SaveInfo save in profile.saves)
        {
            if (save == null ||
                string.IsNullOrWhiteSpace(save.serverName))
            {
                continue;
            }

            if (SaveRepository.Exists(save.serverName))
                existingSaves.Add(save);
        }

        return existingSaves;
    }

    private static Profile CreateDefaultProfile()
    {
        return new Profile
        {
            username = DefaultUsername,
            saves = new List<SaveInfo>()
        };
    }
}
