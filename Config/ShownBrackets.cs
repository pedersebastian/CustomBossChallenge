using System.Collections.Generic;

namespace CustomBossChallenge.Config;

/// <summary>
/// Brackets in a text already in the order it is shown, like the Arabic shaper makes (#244). The shaper turns each
/// bracket around for right-to-left text, also those around Latin words and numbers, so "(Ctrl+Z)" would show as
/// ")Ctrl+Z(". The game's font draws a bracket as it is, so every pair has to face inwards.
/// </summary>
public static class ShownBrackets
{
    private const string Opening = "([", Closing = ")]";

    /// <summary>
    /// <paramref name="shown"/> with every pair of brackets facing inwards: a closing bracket whose partner comes
    /// after it, as seen from the left, is turned around together with that partner
    /// </summary>
    public static string FacingIn(string shown)
    {
        var letters = shown.ToCharArray();
        var waiting = new Stack<int>();
        for (var i = 0; i < letters.Length; i++)
        {
            var closing = Closing.IndexOf(letters[i]);
            var opening = Opening.IndexOf(letters[i]);
            if (closing >= 0)
            {
                // Either the end of a pair that faces inwards, or the start of one turned around
                if (waiting.Count > 0 && letters[waiting.Peek()] == Opening[closing]) waiting.Pop();
                else waiting.Push(i);
            }
            else if (opening >= 0)
            {
                if (waiting.Count > 0 && letters[waiting.Peek()] == Closing[opening])
                {
                    var start = waiting.Pop();
                    letters[start] = Opening[opening];
                    letters[i] = Closing[opening];
                }
                else waiting.Push(i);
            }
        }

        return new string(letters);
    }
}
