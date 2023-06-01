namespace Cheatboard;

public record CompletionResponse(Choice[] choices);
public record Choice(Message message);
public record Message(string role, string content);