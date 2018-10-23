# Goliath

Goliath is the second iteration of the
[Hourai](https://github.com/james7132/Hourai) Discord Bot, written in Python 3.7+.


## Development/Self-Hosting

### Setup

Requirements:

 * Python 3.5.2+
 * pip
 * Google Protobuf Compiler

```sh
virtualenv -p python3 venv
source venv/bin/activate

pip install -r requirements.txt
```

### Launching the Bot

```sh
source venv/bin/activate

# Run the Protobuf compiler
protoc **/*.proto --python_out=.

# Run the bot
python -m hourai
```

### Containerization
```sh
# Build the image
docker build .

# Run the image
```
