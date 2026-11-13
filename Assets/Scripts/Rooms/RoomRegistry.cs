using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "RoomRegistry", menuName = "Project Spear/Rooms/Room Registry")]
public class RoomRegistry : ScriptableObject
{
    [SerializeField] private List<RoomDefinition> rooms = new List<RoomDefinition>();

    public IReadOnlyList<RoomDefinition> Rooms => rooms;

#if UNITY_EDITOR
    public bool Add(RoomDefinition definition)
    {
        if (definition == null || rooms.Contains(definition))
            return false;

        rooms.Add(definition);
        return true;
    }

    public bool Insert(int index, RoomDefinition definition)
    {
        if (definition == null || rooms.Contains(definition))
            return false;

        rooms.Insert(Mathf.Clamp(index, 0, rooms.Count), definition);
        return true;
    }

    public bool RemoveMissingAt(int index)
    {
        if (index < 0 || index >= rooms.Count || rooms[index] != null)
            return false;

        rooms.RemoveAt(index);
        return true;
    }

    public bool RestoreMissingAt(int index)
    {
        if (index < 0 || index > rooms.Count)
            return false;

        rooms.Insert(index, null);
        return true;
    }

    public bool Remove(RoomDefinition definition)
    {
        return rooms.Remove(definition);
    }

    public bool RemoveAt(int index)
    {
        if (index < 0 || index >= rooms.Count)
            return false;

        rooms.RemoveAt(index);
        return true;
    }
#endif
}
