using System.Media;

namespace DeskRhythm.Infrastructure;

/// <summary>One soft chime per reminder. Off by default. Never loops.</summary>
internal static class Chime
{
    private static SoundPlayer? _player;

    public static void Play()
    {
        try
        {
            var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Media", "Windows Notify System Generic.wav");
            if (File.Exists(path))
            {
                // Keep the player alive: Play() is asynchronous.
                _player ??= new SoundPlayer(path);
                _player.Play();
            }
            else
            {
                SystemSounds.Asterisk.Play();
            }
        }
        catch (Exception ex)
        {
            ErrorLog.Write(ex, "Chime");
        }
    }
}
