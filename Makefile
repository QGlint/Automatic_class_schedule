.PHONY: all build test verify run dev run-full clean

CONFIG   ?= Debug
SLN      := Automatic_class_schedule.sln
MAIN     := Automatic_class_schedule\Automatic_class_schedule.csproj
TEST     := Automatic_class_schedule.Tests\Automatic_class_schedule.Tests.csproj
VERIFY   := tests\runtime\WinUIVerify.csproj
VERIFYEXE:= tests\runtime\bin\Debug\net10.0-windows10.0.26100.0\WinUIVerify.exe

# ── Full CI pipeline ──
all: build test verify

# ── Build only (explicit) ──
build:
	dotnet build $(MAIN) -c $(CONFIG) -nologo

# ── Test ──
test-only:
	dotnet test $(TEST) -c $(CONFIG) -nologo

test: build
	dotnet test $(TEST) -c $(CONFIG) --no-build -nologo

# ── Verify ──
verify: build
	dotnet build $(VERIFY) -c $(CONFIG) -nologo
	$(VERIFYEXE)

# ── Run: 先编译再启动 ──
run: build
	dotnet run --project $(MAIN) -c $(CONFIG) --no-build -nologo

# ── Dev mode: 热重载，修改代码后自动刷新 ──
dev:
	dotnet watch run --project $(MAIN) -c $(CONFIG) -nologo

# ── Full run: 先编译再启动 ──
run-full: build
	dotnet run --project $(MAIN) -c $(CONFIG) --no-build -nologo

clean:
	dotnet clean $(SLN) -nologo
