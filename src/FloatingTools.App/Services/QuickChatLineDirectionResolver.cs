using FloatingTools.App.SharedUi.Direction;

namespace FloatingTools.App.Services;

public static class QuickChatLineDirectionResolver
{
    public static TextDirectionResolution Resolve(string? line)
    {
        var firstStrong = TextDirectionResolver.Resolve(line);
        if (firstStrong.Direction != TextDirection.LeftToRight)
        {
            return firstStrong;
        }

        var leftToRightTokens = 0;
        var rightToLeftTokens = 0;
        foreach (var token in line!.Split(
                     (char[]?)null,
                     StringSplitOptions.RemoveEmptyEntries))
        {
            switch (TextDirectionResolver.Resolve(token).Direction)
            {
                case TextDirection.LeftToRight:
                    leftToRightTokens++;
                    break;
                case TextDirection.RightToLeft:
                    rightToLeftTokens++;
                    break;
            }
        }

        return rightToLeftTokens > leftToRightTokens
            ? new TextDirectionResolution(
                TextDirection.RightToLeft,
                TextDirectionAlignment.Right)
            : firstStrong;
    }
}
