.PHONY: all build test verify run clean

CONFIG   ?= Debug
SLN      := Automatic_class_schedule.sln
MAIN     := Automatic_class_schedule\Automatic_class_schedule.csproj
TEST     := Automatic_class_schedule.Tests\Automatic_class_schedule.Tests.csproj
VERIFY   := tests\runtime\WinUIVerify.csproj
VERIFYEXE:= tests\runtime\bin\Debug\net10.0-windows10.0.26100.0\WinUIVerify.exe

all: build test verify

build:
	dotnet build $(MAIN) -c $(CONFIG) -nologo

test-only:
	dotnet test $(TEST) -c $(CONFIG) -nologo

test: build
	dotnet test $(TEST) -c $(CONFIG) --no-build -nologo

verify: build
	dotnet build $(VERIFY) -c $(CONFIG) -nologo
	$(VERIFYEXE)

run: build
	dotnet run --project $(MAIN) -c $(CONFIG) -nologo

clean:
	dotnet clean $(SLN) -nologo
