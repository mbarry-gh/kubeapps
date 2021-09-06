.PHONY: build

build :
	-dotnet tool uninstall -g kubeapps
	dotnet pack --version-suffix beta1
	dotnet tool install -g kubeapps --add-source nupkg --version 0.1.0-beta1
