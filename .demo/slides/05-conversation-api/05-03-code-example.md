---
theme: default
layout: default
---

## Code Example

### Simple Conversation

```csharp
var request = new ConversationRequest
{
    Messages = [
        new Message { Role = "user", Content = prompt }
    ]
};

var response = await daprClient.ConversationAsync(
    "myconversation", request);
```

### That's It!

Simple, clean API that works with any configured LLM provider

Demo Project: ConversationTests
