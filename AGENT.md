---
version: "1.0"
name: "VoidSniffer"
description: "AI-агент для разработки приложения визуализации дискового пространства (treemap) на основе индекса Everything (voidtools)"
role: "Senior .NET Developer"
context: |
  Разработка Windows Forms приложения на C# (.NET Framework 4.8) для визуализации 
  использования дискового пространства в виде Treemap. Основное отличие:
  данные берутся из индекса Everything (voidtools), а не из 
  собственного сканирования файловой системы.
priorities:
  - "Корректная интеграция с Everything SDK/IPC"
  - "Построение иерархии файловой системы из плоского индекса"
  - "Эффективная визуализация Treemap для больших объёмов данных"
  - "Интерактивность: zoom, навигация, контекстное меню"
tech_stack:
  - ".NET Framework 4.8"
  - "C# 7.3+"
  - "Windows Forms"
  - "NuGet-пакеты (EverythingSearchClient, Voidtools.Everything.Net или прямая P/Invoke)"
  - "Библиотека Treemap (FastReport Business Graphics .NET или кастомный контрол)"
rules:
  - "Всегда проверять, запущен ли сервис Everything, перед запросом"
  - "Использовать non-blocking режим для длительных запросов"
  - "Ограничивать maxResults для предотвращения переполнения памяти"
  - "Кэшировать результаты запросов при навигации"
  - "Использовать AnyCPU сборку, если возможно"
change_policy:
  - "Любые изменения в интеграции с Everything должны сопровождаться тестами на отсутствие сервиса"
  - "Визуальные изменения проверять на производительность с >100k узлов"
---

## Setup

### Требования
- Visual Studio 2019 (16.11+) или Visual Studio 2022 с workload **".NET desktop development"**
- .NET Framework 4.8 Developer Pack / Targeting Pack
- Установленный Everything (voidtools) с запущенным сервисом (для runtime)
- NuGet Package Manager

### Создание проекта

#### Вариант 1 — через Visual Studio (рекомендуется)

1. **File → New → Project**
2. Выбрать шаблон **"Windows Forms App (.NET Framework)"** (C#)
   - Не путать с "Windows Forms App" (без пометки .NET Framework) — это .NET 5+
3. **Project name:** `VoidSniffer`
4. **Location:** выбрать рабочую папку
5. **Framework:** `.NET Framework 4.8`
6. Нажать **Create**

#### Вариант 2 — вручную (если нет Visual Studio)

Создать папку `VoidSniffer` и файл `VoidSniffer.csproj`:

```xml
<?xml version="1.0" encoding="utf-8"?>
<Project ToolsVersion="15.0" xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
  <Import Project="$(MSBuildExtensionsPath)\$(MSBuildToolsVersion)\Microsoft.Common.props"
          Condition="Exists('$(MSBuildExtensionsPath)\$(MSBuildToolsVersion)\Microsoft.Common.props')" />
  <PropertyGroup>
    <Configuration Condition=" '$(Configuration)' == '' ">Debug</Configuration>
    <Platform Condition=" '$(Platform)' == '' ">AnyCPU</Platform>
    <ProjectGuid>{GENERATE-NEW-GUID}</ProjectGuid>
    <OutputType>WinExe</OutputType>
    <RootNamespace>VoidSniffer</RootNamespace>
    <AssemblyName>VoidSniffer</AssemblyName>
    <TargetFrameworkVersion>v4.8</TargetFrameworkVersion>
    <FileAlignment>512</FileAlignment>
    <AutoGenerateBindingRedirects>true</AutoGenerateBindingRedirects>
    <Deterministic>true</Deterministic>
  </PropertyGroup>
  <PropertyGroup Condition=" '$(Configuration)|$(Platform)' == 'Debug|AnyCPU' ">
    <DebugSymbols>true</DebugSymbols>
    <DebugType>full</DebugType>
    <Optimize>false</Optimize>
    <OutputPath>bin\Debug\</OutputPath>
    <DefineConstants>DEBUG;TRACE</DefineConstants>
    <ErrorReport>prompt</ErrorReport>
    <WarningLevel>4</WarningLevel>
  </PropertyGroup>
  <PropertyGroup Condition=" '$(Configuration)|$(Platform)' == 'Release|AnyCPU' ">
    <DebugType>pdbonly</DebugType>
    <Optimize>true</Optimize>
    <OutputPath>bin\Release\</OutputPath>
    <DefineConstants>TRACE</DefineConstants>
    <ErrorReport>prompt</ErrorReport>
    <WarningLevel>4</WarningLevel>
  </PropertyGroup>
  <ItemGroup>
    <Reference Include="System" />
    <Reference Include="System.Core" />
    <Reference Include="System.Drawing" />
    <Reference Include="System.Windows.Forms" />
    <Reference Include="System.Xml" />
  </ItemGroup>
  <ItemGroup>
    <Compile Include="Program.cs" />
    <Compile Include="MainForm.cs">
      <SubType>Form</SubType>
    </Compile>
    <Compile Include="MainForm.Designer.cs">
      <DependentUpon>MainForm.cs</DependentUpon>
    </Compile>
    <Compile Include="Properties\AssemblyInfo.cs" />
  </ItemGroup>
  <Import Project="$(MSBuildToolsPath)\Microsoft.CSharp.targets" />
</Project>
```

Файл `packages.config` (для NuGet в формате packages.config — стандарт для .NET Framework):

```xml
<?xml version="1.0" encoding="utf-8"?>
<packages>
  <package id="EverythingSearchClient" version="X.Y.Z" targetFramework="net48" />
</packages>
```

### Установка NuGet-пакетов

Есть два способа — оба работают с .NET Framework 4.8:

#### Через Visual Studio

**Tools → NuGet Package Manager → Package Manager Console:**

```powershell
Install-Package EverythingSearchClient
# опционально для Treemap:
Install-Package FastReport.BusinessGraphics.NET
```

Либо **Tools → NuGet Package Manager → Manage NuGet Packages for Solution** → Browse → установить нужное.

#### Через CLI (dotnet CLI работает с .NET Framework, если проект уже создан)

```powershell
cd VoidSniffer
dotnet add package EverythingSearchClient --version X.Y.Z
```

**Важно:** `dotnet add package` работает с уже существующим `.csproj`. Просто создать .NET Framework проект командой `dotnet new` нельзя — нужных шаблонов в SDK нет. Сборка и запуск — через `msbuild` или `dotnet build` (последний тоже умеет собирать .NET Framework проекты при наличии MSBuild и Targeting Pack).

Сборка из CLI:

```powershell
msbuild VoidSniffer.csproj /p:Configuration=Release
# или
dotnet build VoidSniffer.csproj -c Release
```

### Проверка окружения

Перед запуском убедитесь, что Everything работает:

```c#
using EverythingSearchClient;
var client = new SearchClient();
var result = client.Search("C:\\", SearchFlags.None, 1);
```

------

## Architecture

### Компоненты

1. **EverythingDataProvider** — обёртка над Everything SDK/IPC
2. **FileSystemTreeBuilder** — преобразование плоского списка в иерархию
3. **TreemapRenderer** — визуализация иерархии
4. **NavigationController** — управление zoom, back/forward, home
5. **FilterService** — фильтрация по имени, размеру, дате

### Поток данных

```text
Everything Index → EverythingDataProvider (IPC/SDK) 
    → FileSystemTreeBuilder (построение дерева) 
    → TreemapRenderer (отрисовка) 
    → UI (WinForms)
```

------

## Code Style

- Использовать `async/await` для всех вызовов Everything IPC
- Все P/Invoke вызовы изолировать в отдельный класс
- Избегать блокировки UI-потока
- Использовать `IDisposable` для ресурсов (окна, GDI+ объекты)
- Логировать ошибки через `System.Diagnostics.Trace`

------

## Testing

- Модульные тесты для `FileSystemTreeBuilder` (преобразование плоского списка в дерево)
- Интеграционные тесты с mock-сервисом Everything
- Тесты на производительность: построение дерева для 500k+ файлов
- UI-тесты: проверка zoom, навигации, контекстного меню

------

## Security

- Не запрашивать права администратора без необходимости
- Валидировать все пути перед передачей в Everything
- Не выполнять команды из контекстного меню без подтверждения пользователя