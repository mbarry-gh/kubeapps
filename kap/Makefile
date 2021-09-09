.PHONY: build

build :
	-dotnet tool uninstall -g kubeapps
	dotnet pack --version-suffix ""
	dotnet tool install -g kubeapps --add-source nupkg
