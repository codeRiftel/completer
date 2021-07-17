.PHONY: build

build:
	mcs option/*cs main.cs Completer.cs -out:completer.exe
