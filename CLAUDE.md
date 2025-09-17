# IndexContainment - Claude Code Assistant Guide

## Project Overview

**IndexContainment** is a .NET 8+ console application for financial data analysis, specifically designed to test the "Half-Gap Containment" hypothesis using 25 years of index minute data. The application processes CSV data, performs statistical analysis, and exports results to Excel format.

## Key Purpose
- Ingests 25 years of index minute data (~30 bars/day per symbol)
- Reduces data to compact per-day datasets
- Computes threshold statistics for Half-Gap Containment hypothesis
- Exports analysis to single .xlsx workbook with one sheet per symbol

## Project Structure

```
IndexContainment/
├─ ZEN/                    # Main source code (renamed from 'src')
│  ├─ IndexContainment.sln # Visual Studio solution file
│  ├─ Core/               # Models, constants, utilities
│  ├─ Data/               # CSV data loading logic
│  ├─ Analysis/           # Data reductions + summary statistics
│  ├─ Excel/              # ClosedXML Excel writer
│  ├─ Cli/                # Console application entry point
│  ├─ Tests/              # Unit tests with synthetic data
│  └─ scripts/            # Build and run helper scripts
├─ DATA/                  # Input CSV files (git ignored)
├─ OUTPUT/                # Excel output files
├─ PATCHES/               # Date/time-wise patches directory
├─ WPS/                   # Work Packages documentation (12 files)
└─ Analysis/              # Performance notes and analysis docs
```

## Work Packages (WPS Directory)

The project is organized into 12 work packages, each with placeholder documentation:

1. **WP1_Data_Contract.md** - Data input/output specifications
2. **WP2_Directory_Layout.md** - Project structure and organization
3. **WP3_Loader_Session_Guardrails.md** - Data loading safety mechanisms
4. **WP4_Anchor_Reductions.md** - Time anchor processing logic
5. **WP5_Derived_Metrics.md** - Calculated metrics and indicators
6. **WP6_Threshold_Logic.md** - Containment threshold calculations
7. **WP7_Symbol_Level_Analysis.md** - Per-symbol statistical analysis
8. **WP8_Excel_Writer.md** - Excel output formatting
9. **WP9_Config_Metadata.md** - Configuration and metadata handling
10. **WP10_Performance.md** - Performance optimization strategies
11. **WP11_Validation_Logs.md** - Data validation and logging
12. **WP12_CLI_ExitCodes.md** - Command-line interface error handling

## Data Format

### Input CSV Schema
Each bar contains: `Date,Time,Open,High,Low,Close,Volume`
- `Date`: yyyy-MM-dd (exchange local date)
- `Time`: HH:mm (exchange local time, consistent timezone)
- Expected ~30 RTH bars per day (e.g., 13-minute aggregation)
- Half-days acceptable

### Analysis Parameters
- **Anchor Time**: 10:00 (last bar ≤ 10:00 used)
- **Threshold Grid**: {1.0%, 1.5%, 2.0%, 3.0%, 4.0%}
- **Containment Logic**: Extra drop ≤ ½·|X| after 10:00

## Build and Run Instructions

### Prerequisites
- .NET 8 or later
- ClosedXML NuGet package for Excel generation

### Quick Start
```bash
# Check .NET version
dotnet --version

# Build the solution
cd ZEN
dotnet build

# Run with default parameters
dotnet run --project Cli -- --data ../DATA --out ../OUTPUT/IndexContainment.xlsx --symbols SPY,QQQ,IWM,DIA --anchor 10:00

# Alternative: Use helper scripts
# Windows PowerShell
ZEN/scripts/run.ps1

# Unix/Linux/macOS
ZEN/scripts/run.sh
```

### Command Line Arguments
- `--data`: Path to input CSV directory
- `--out`: Output Excel file path
- `--symbols`: Comma-separated list of symbols to process
- `--anchor`: Anchor time (default: 10:00)

## Output Format

### Excel Workbook Structure
- **Config Sheet**: Metadata and run parameters
- **Symbol Sheets**: One per analyzed symbol containing:
  - Summary block per threshold:
    - n (sample size)
    - hits (successful containments)
    - hitRate (success percentage)
    - WilsonLower95 (confidence interval)
    - p99ViolationRatio (99th percentile violations)
    - median time-to-low
  - Daily compact table (filterable)

## Development Notes

### Technical Constraints
- Each symbol's 25-year dataset fits within single Excel sheet
- Excel row limit ~1,048,576 (project stays well below)
- Loader skips days with <20 bars (logged)
- Handles half-day sessions with different coverage

### Code Organization
- **Separation of Concerns**: Each project handles specific functionality
- **Testability**: Unit tests use synthetic data
- **Performance**: Optimized for large dataset processing
- **Configuration**: Centralized parameter management

## Patches Directory

The `PATCHES/` directory is prepared for date/time-wise patches that will be applied to enhance the codebase functionality.

## Getting Help

For development assistance:
1. Review work package documentation in WPS/
2. Check existing unit tests in ZEN/Tests/
3. Examine CSV data format requirements
4. Review Excel output specifications

## Development Workflow

1. **Data Preparation**: Place CSV files in DATA/ directory
2. **Build**: Use `dotnet build` in ZEN/ directory
3. **Test**: Run unit tests before major changes
4. **Execute**: Use CLI with appropriate parameters
5. **Verify**: Check Excel output in OUTPUT/ directory

## Performance Considerations

- Designed for 25 years of minute-level data per symbol
- Efficient memory usage for large datasets
- Optimized Excel generation with ClosedXML
- Configurable processing parameters for different use cases