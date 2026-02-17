using System.Collections.Generic;
using System.Threading.Tasks;

namespace FileSurfer.Core.ViewModels;

public interface IDialogService
{
    public void InfoDialog(string title, string info);

    public Task<bool> ConfirmationDialog(string title, string question);

    public Task<string?> InputDialog(string title, string context, bool secret);

    public Task<string?> SuggestInputDialog(
        string title,
        string context,
        string suggestionLabel,
        IReadOnlyList<string> options
    );
}
