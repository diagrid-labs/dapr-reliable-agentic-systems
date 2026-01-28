---
layout: section
customTheme: .demo/slides/theme/theme.css
---

# Dapr Conversation API

![Animation](.demo/images/bot-animations-3.gif)

---
layout: default
customTheme: .demo/slides/theme/theme.css
---

![Conversation API](.demo/images/dapr-conversation-api.png)

---
layout: default
customTheme: .demo/slides/theme/theme.css
---

# Conversation API code example

```csharp
var response = await _conversationClient.ConverseAsync(
  [
    new ConversationInput(new List<IConversationMessage>
    {
      new SystemMessage
      {
        Content = [new MessageContent(@"You are a English to Klingon translator.")]
      },
      new UserMessage
      {
        Content = [new MessageContent($"Translate this to Klingon: {message}")]
      }
    })
  ],
  new ConversationOptions("myconversation")
  {
    Temperature = 0.7
  }
);
```

---
layout: default
customTheme: .demo/slides/theme/theme.css
---

# Conversation component example

```yaml
apiVersion: dapr.io/v1alpha1
kind: Component
metadata:
  name: myconversation
spec:
  type: conversation.openai
  metadata:
  - name: key
    value: <mykey>
  - name: model
    value: gpt-4-turbo
  - name: endpoint
    value: 'https://api.openai.com/v1'
  - name: cacheTTL
    value: 10m
```