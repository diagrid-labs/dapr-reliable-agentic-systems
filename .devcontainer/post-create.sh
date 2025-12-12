wget -q https://raw.githubusercontent.com/dapr/cli/master/install/install.sh -O - | /bin/bash
dapr uninstall
dapr init

curl -fsSL https://ollama.com/install.sh | sh