---
layout: section
---

# Dapr Conversation API

![Animation](.demo/images/bot-animations-3.gif)

---
layout: default
---

![Conversation API](.demo/images/dapr-conversation-api.png)

---
layout: default
---

# Conversation API code example

```csharp
// DaprConversationClient is injected via the constructor
var response = await _conversationClient.ConverseAsync(
  [
    new ConversationInput(new List<IConversationMessage>
    {
      new SystemMessage
      {
        Content = [new MessageContent("You are Data's scientific classification system. Return only the classification type.")]
      },
      new UserMessage
      {
        Content = [new MessageContent($"Classify this anomaly: {processedData}")]
      }
    })
  ],
  new ConversationOptions("conversation") { Temperature = 0.3 });

var classification = response.Outputs.First().Choices.First().Message.Content.Trim();
```

---
layout: default
---

# Conversation component example

```yaml
apiVersion: dapr.io/v1alpha1
kind: Component
metadata:
  name: conversation
spec:
  type: conversation.openai
  version: v1
  metadata:
    - name: key
      secretKeyRef:
        name: OPENAI_API_KEY
        key: OPENAI_API_KEY
    - name: model
      value: gpt-4.1-mini
auth:
  secretStore: envsecretstore
```
