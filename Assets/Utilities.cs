using UnityEngine;

public static class Utilities
{
    public static bool AlmostEquals(this Vector3 vector, Vector3 other)
    {
        var boundsA = new Bounds(vector, Vector3.one * 0.04f);
        var boundsB = new Bounds(other, Vector3.one * 0.04f);
        if (boundsA.Intersects(boundsB))
        {
            return true;
        }

        return false;
    }

}