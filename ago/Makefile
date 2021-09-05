build :
	-dotnet tool uninstall -g autogitops
	cd src && dotnet pack --version-suffix beta
	dotnet tool install -g autogitops --add-source src/nupkg --version 0.1.0-beta
