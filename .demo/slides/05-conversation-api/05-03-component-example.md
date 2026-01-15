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