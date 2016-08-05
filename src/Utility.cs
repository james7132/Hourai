using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Discord;

namespace DrumBot {


    public static class Utility {

        //TODO: Expose as config option
        const int MaxRetries = 20;

        public static bool RoleCheck(User user, Role role) {
            int position = role.Position;
            return user.IsServerOwner() || user.Roles.Max(r => r.Position) > position;
        }

        public static string DateString(DateTime date) {
            return date.ToString("yyyy-MM-dd HH:mm:ss");
        }

        public static async Task FileIO(Func<Task> fileIOaction,
                                        Action retry = null,
                                        Action failure = null) {
            var success = false;
            var tries = 0;
            while (!success) {
                try {
                    await fileIOaction();
                    success = true;
                } catch (IOException) {
                    if (tries <= MaxRetries) {
                        retry?.Invoke();
                        tries++;
                        await Task.Delay(100);
                    }
                    else {
                        Log.Error(
                            "Failed to read file for search. Max retries exceeded.");
                        failure?.Invoke();
                        throw;
                    }
                }
            }
        }

    }
}
