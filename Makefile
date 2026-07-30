.PHONY: all build test verify run dev run-full clean publish-zip publish-store manifest

CONFIG   ?= Debug
SLN      := Automatic_class_schedule.sln
MAIN     := Automatic_class_schedule\Automatic_class_schedule.csproj
TEST     := Automatic_class_schedule.Tests\Automatic_class_schedule.Tests.csproj
VERIFY   := tests\runtime\WinUIVerify.csproj
VERIFYEXE:= tests\runtime\bin\Debug\net10.0-windows10.0.26100.0\WinUIVerify.exe
VERSION  ?= 1.0.0
MANIFEST := Automatic_class_schedule\Package.appxmanifest
TEMPLATE := Automatic_class_schedule\Package.appxmanifest.template

# ── Manifest: 从模板 + publish.conf 生成 ──
manifest:
	@if not exist publish.conf (echo [ERROR] Missing publish.conf && exit /b 1)
	powershell -NoProfile -Command "\
		$$conf = @{}; \
		Get-Content publish.conf | ForEach-Object { if ($$_ -match '^([^#][^=]+)=(.*)$$') { $$conf[$$matches[1].Trim()] = $$matches[2].Trim() } }; \
		$$c = Get-Content '$(TEMPLATE)' -Raw -Encoding UTF8; \
		$$c = $$c -replace '\{\{PUBLISHER\}\}', $$conf['PUBLISHER']; \
		$$c = $$c -replace '\{\{AUTHOR\}\}', $$conf['AUTHOR']; \
		$$c = $$c -replace '\{\{VERSION\}\}', '$(VERSION).0'; \
		[System.IO.File]::WriteAllText('$(MANIFEST)', $$c, [System.Text.UTF8Encoding]::new($$false)); \
		Write-Host 'Manifest generated: Publisher=' $$conf['PUBLISHER'] ', Version=$(VERSION).0'"

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

# ── Publish: ZIP 自包含发布 ──
# 用法: make publish-zip VERSION=1.2.0
publish-zip:
	dotnet publish $(MAIN) -c Release -r win-x64 --self-contained true \
		-p:WindowsPackageType=None \
		-p:PublishSingleFile=false \
		-p:DebugType=none -p:DebugSymbols=false \
		-o publish\app
	powershell -NoProfile -Command "Compress-Archive -Path 'publish\app\*' -DestinationPath 'publish\ACS_v$(VERSION)_win-x64.zip' -Force; Remove-Item 'publish\app' -Recurse -Force"
	@echo === Output: publish\ACS_v$(VERSION)_win-x64.zip ===

# ── Publish: Store MSIX ──
# 用法: make publish-store VERSION=1.2.0
publish-store: manifest
	dotnet build $(MAIN) -c Release -r win-x64 \
		-p:WindowsPackageType=MSIX \
		-p:UapAppxPackageBuildMode=StoreUpload \
		-p:AppxPackageSigningEnabled=false \
		-p:DebugType=none -p:DebugSymbols=false \
		"-t:Build;_GenerateAppxPackage"
	powershell -NoProfile -Command "New-Item publish -ItemType Directory -Force | Out-Null; Copy-Item 'Automatic_class_schedule\AppPackages\*.msixupload' 'publish\' -Force"
	@echo === Output: publish\*.msixupload ===

clean:
	dotnet clean $(SLN) -nologo
