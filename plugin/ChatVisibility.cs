using System.Text.Json;
namespace HudWorkshop;
public sealed partial class Plugin
{
    private HashSet<string> disabledChat=new();
    private ulong chatVisibilityOwner;
    private string ChatVisibilityPath=>Path.Combine(PluginInterface.GetPluginConfigDirectory(),$"chat-visibility-{PlayerState.ContentId:X}.json");
    private void ApplyChatVisibility()
    {
        if(!ClientState.IsLoggedIn)return;
        if(chatVisibilityOwner!=PlayerState.ContentId)
        {
            chatVisibilityOwner=PlayerState.ContentId;
            try{disabledChat=File.Exists(ChatVisibilityPath)?JsonSerializer.Deserialize<HashSet<string>>(File.ReadAllText(ChatVisibilityPath))??new():new();}
            catch(Exception ex){disabledChat=new();Log.Warning(ex,"Cannot read chat visibility");}
        }
        foreach(var id in disabledChat.Concat(previewEnabled.Where(p=>IsChat(p.Key)&&!p.Value).Select(p=>p.Key)).Distinct().Where(id=>IsChat(id)&&Names.Contains(id)))
            if(!previewEnabled.TryGetValue(id,out var enabled)||!enabled)EnableNative(id,false);
    }
    private void SaveChatVisibility(Dictionary<string,bool> changes)
    {
        if(!changes.Keys.Any(IsChat))return;
        var updated=new HashSet<string>(disabledChat);
        foreach(var (id,enabled) in changes.Where(p=>IsChat(p.Key)))
            if(enabled)updated.Remove(id);else updated.Add(id);
        Directory.CreateDirectory(PluginInterface.GetPluginConfigDirectory());
        File.WriteAllText(ChatVisibilityPath+".tmp",JsonSerializer.Serialize(updated));
        File.Move(ChatVisibilityPath+".tmp",ChatVisibilityPath,true);disabledChat=updated;
    }
}
