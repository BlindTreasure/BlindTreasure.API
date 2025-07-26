#!/bin/bash

# BlindTreasure Unit Test Runner for Linux/Mac
# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
CYAN='\033[0;36m'
NC='\033[0m' # No Color

# Default values
CONFIGURATION="Release"
OUTPUT_DIR="./TestResults"
REPORTS_DIR="./Reports"
SKIP_BUILD=false
OPEN_REPORT=true
PROJECT_NAME="BlindTreasure API"
VERSION="local-dev"

# Functions
print_message() {
    local color=$1
    local message=$2
    echo -e "${color}${message}${NC}"
}

print_error() {
    print_message $RED "❌ $1"
}

print_success() {
    print_message $GREEN "✅ $1"
}

print_info() {
    print_message $CYAN "ℹ️ $1"
}

print_warning() {
    print_message $YELLOW "⚠️ $1"
}

check_prerequisites() {
    print_info "Checking prerequisites..."
    
    # Check .NET SDK
    if command -v dotnet &> /dev/null; then
        local dotnet_version=$(dotnet --version)
        print_success ".NET SDK version: $dotnet_version"
    else
        print_error ".NET SDK not found! Please install .NET SDK 8.0 or later."
        exit 1
    fi
    
    # Check Python
    if command -v python3 &> /dev/null; then
        local python_version=$(python3 --version)
        print_success "Python version: $python_version"
        return 0
    elif command -v python &> /dev/null; then
        local python_version=$(python --version)
        print_success "Python version: $python_version"
        return 0
    else
        print_warning "Python not found. Excel report will not be generated."
        return 1
    fi
}

install_python_dependencies() {
    print_info "Installing Python dependencies..."
    
    local python_cmd="python3"
    if ! command -v python3 &> /dev/null; then
        python_cmd="python"
    fi
    
    local packages=("pandas" "openpyxl" "lxml" "beautifulsoup4")
    
    for package in "${packages[@]}"; do
        print_info "Installing $package..."
        if $python_cmd -m pip install "$package" --quiet; then
            print_success "$package installed"
        else
            print_error "Failed to install $package"
            return 1
        fi
    done
    
    return 0
}

clean_directories() {
    print_info "Cleaning directories..."
    
    if [ -d "$OUTPUT_DIR" ]; then
        rm -rf "$OUTPUT_DIR"
        print_success "Cleaned $OUTPUT_DIR"
    fi
    
    if [ -d "$REPORTS_DIR" ]; then
        rm -rf "$REPORTS_DIR"
        print_success "Cleaned $REPORTS_DIR"
    fi
    
    # Create directories
    mkdir -p "$OUTPUT_DIR"
    mkdir -p "$REPORTS_DIR"
}

build_solution() {
    if [ "$SKIP_BUILD" = true ]; then
        print_warning "Skipping build step..."
        return 0
    fi
    
    print_info "Building solution..."
    
    # Restore packages
    print_info "Restoring NuGet packages..."
    if ! dotnet restore "./BlindTreasure.API.sln"; then
        print_error "Package restore failed!"
        return 1
    fi
    
    # Build solution
    print_info "Building solution..."
    if ! dotnet build "./BlindTreasure.API.sln" --configuration "$CONFIGURATION" --no-restore; then
        print_error "Build failed!"
        return 1
    fi
    
    print_success "Build completed successfully!"
    return 0
}

run_tests() {
    print_info "Running unit tests..."
    
    local test_project="./BlindTreaure.UnitTest/BlindTreaure.UnitTest.csproj"
    
    if [ ! -f "$test_project" ]; then
        print_error "Test project not found: $test_project"
        return 1
    fi
    
    # Run tests with coverage
    if ! dotnet test "$test_project" \
        --configuration "$CONFIGURATION" \
        --no-build \
        --verbosity normal \
        --logger "trx;LogFileName=test-results.trx" \
        --logger "console;verbosity=detailed" \
        --logger "junit;LogFileName=junit-results.xml" \
        --collect:"XPlat Code Coverage" \
        --results-directory "$OUTPUT_DIR" \
        --settings ./coverlet.runsettings; then
        print_warning "Some tests failed, but continuing with report generation..."
    else
        print_success "All tests passed!"
    fi
    
    return 0
}

generate_coverage_report() {
    print_info "Generating coverage report..."
    
    # Install ReportGenerator if not already installed
    if ! command -v reportgenerator &> /dev/null; then
        print_info "Installing ReportGenerator..."
        dotnet tool install -g dotnet-reportgenerator-globaltool
        
        # Update PATH for current session
        export PATH="$PATH:$HOME/.dotnet/tools"
    fi
    
    # Find coverage files
    local coverage_files=$(find "$OUTPUT_DIR" -name "coverage.opencover.xml" -type f)
    
    if [ -z "$coverage_files" ]; then
        print_warning "No coverage files found"
        return 1
    fi
    
    # Convert to semicolon-separated list
    local coverage_reports=$(echo "$coverage_files" | tr '\n' ';' | sed 's/;$//')
    
    # Generate coverage report
    if reportgenerator \
        "-reports:$coverage_reports" \
        "-targetdir:$REPORTS_DIR/coverage" \
        "-reporttypes:Html;Cobertura;JsonSummary;Badges" \
        "-title:$PROJECT_NAME Coverage Report" \
        "-tag:local-$(date +%Y-%m-%d-%H-%M)"; then
        print_success "Coverage report generated!"
        return 0
    else
        print_error "Coverage report generation failed!"
        return 1
    fi
}

generate_excel_report() {
    local has_python=$1
    
    if [ "$has_python" = false ]; then
        print_warning "Skipping Excel generation (Python not available)"
        return 1
    fi
    
    print_info "Generating Excel report..."
    
    local script_path="./.github/scripts/convert-tests-to-excel.py"
    
    if [ ! -f "$script_path" ]; then
        print_error "Excel generation script not found: $script_path"
        return 1
    fi
    
    local current_branch=$(git branch --show-current 2>/dev/null || echo "local")
    local build_number="local-$(date +%Y%m%d-%H%M)"
    local output_file="$PROJECT_NAME-TestReport-$build_number.xlsx"
    
    local python_cmd="python3"
    if ! command -v python3 &> /dev/null; then
        python_cmd="python"
    fi
    
    if $python_cmd "$script_path" \
        --test-results-dir "$OUTPUT_DIR" \
        --reports-dir "$REPORTS_DIR" \
        --output-file "$output_file" \
        --project-name "$PROJECT_NAME" \
        --version "$VERSION" \
        --branch "$current_branch" \
        --build-number "$build_number"; then
        print_success "Excel report generated: $output_file"
        echo "$output_file"
        return 0
    else
        print_error "Excel generation failed!"
        return 1
    fi
}

show_summary() {
    local excel_file=$1
    local has_coverage=$2
    
    echo
    print_info "$(printf '=%.0s' {1..60})"
    print_info "📊 TEST EXECUTION SUMMARY"
    print_info "$(printf '=%.0s' {1..60})"
    
    # Count test results from TRX files
    local trx_files=$(find "$OUTPUT_DIR" -name "*.trx" -type f | head -1)
    
    if [ -n "$trx_files" ] && [ -f "$trx_files" ]; then
        # Simple XML parsing using grep and sed (works on most systems)
        local counters=$(grep -o 'total="[0-9]*"' "$trx_files" | head -1 | sed 's/total="\([0-9]*\)"/\1/')
        local passed=$(grep -o 'passed="[0-9]*"' "$trx_files" | head -1 | sed 's/passed="\([0-9]*\)"/\1/')
        local failed=$(grep -o 'failed="[0-9]*"' "$trx_files" | head -1 | sed 's/failed="\([0-9]*\)"/\1/')
        
        if [ -n "$counters" ] && [ "$counters" -gt 0 ]; then
            local pass_rate=$(echo "scale=2; $passed * 100 / $counters" | bc -l 2>/dev/null || echo "N/A")
            
            print_info "🧪 Total Tests: $counters"
            print_success "✅ Passed: $passed"
            if [ "$failed" -gt 0 ]; then
                print_error "❌ Failed: $failed"
            else
                print_success "❌ Failed: $failed"
            fi
            print_info "📈 Pass Rate: $pass_rate%"
        fi
    fi
    
    echo
    print_info "📁 Generated Files:"
    
    if [ -d "$OUTPUT_DIR" ]; then
        print_info "  📄 Test Results: $OUTPUT_DIR"
    fi
    
    if [ "$has_coverage" = true ] && [ -f "$REPORTS_DIR/coverage/index.html" ]; then
        print_info "  📊 Coverage Report: $REPORTS_DIR/coverage/index.html"
    fi
    
    if [ -n "$excel_file" ] && [ -f "$excel_file" ]; then
        print_success "  📋 Excel Report: $excel_file"
    fi
    
    print_info "$(printf '=%.0s' {1..60})"
}

open_reports() {
    local excel_file=$1
    local has_coverage=$2
    
    if [ "$OPEN_REPORT" = false ]; then
        return
    fi
    
    print_info "🚀 Opening reports..."
    
    # Open Excel report
    if [ -n "$excel_file" ] && [ -f "$excel_file" ]; then
        if command -v open &> /dev/null; then
            # macOS
            open "$excel_file" && print_success "Opened Excel report"
        elif command -v xdg-open &> /dev/null; then
            # Linux
            xdg-open "$excel_file" && print_success "Opened Excel report"
        else
            print_warning "Could not open Excel report automatically"
        fi
    fi
    
    # Open coverage report
    if [ "$has_coverage" = true ] && [ -f "$REPORTS_DIR/coverage/index.html" ]; then
        if command -v open &> /dev/null; then
            # macOS
            open "$REPORTS_DIR/coverage/index.html" && print_success "Opened coverage report"
        elif command -v xdg-open &> /dev/null; then
            # Linux
            xdg-open "$REPORTS_DIR/coverage/index.html" && print_success "Opened coverage report"
        else
            print_warning "Could not open coverage report automatically"
        fi
    fi
}

# Parse command line arguments
while [[ $# -gt 0 ]]; do
    case $1 in
        --configuration)
            CONFIGURATION="$2"
            shift 2
            ;;
        --output-dir)
            OUTPUT_DIR="$2"
            shift 2
            ;;
        --reports-dir)
            REPORTS_DIR="$2"
            shift 2
            ;;
        --skip-build)
            SKIP_BUILD=true
            shift
            ;;
        --no-open)
            OPEN_REPORT=false
            shift
            ;;
        --project-name)
            PROJECT_NAME="$2"
            shift 2
            ;;
        --version)
            VERSION="$2"
            shift 2
            ;;
        --help)
            echo "Usage: $0 [OPTIONS]"
            echo "Options:"
            echo "  --configuration <config>   Build configuration (default: Release)"
            echo "  --output-dir <dir>         Test results directory (default: ./TestResults)"
            echo "  --reports-dir <dir>        Reports directory (default: ./Reports)"
            echo "  --skip-build               Skip build step"
            echo "  --no-open                  Don't open reports automatically"
            echo "  --project-name <name>      Project name for reports"
            echo "  --version <version>        Version string"
            echo "  --help                     Show this help"
            exit 0
            ;;
        *)
            print_error "Unknown option: $1"
            exit 1
            ;;
    esac
done

# Main execution
main() {
    print_info "🚀 Starting BlindTreasure Unit Test Execution"
    print_info "Configuration: $CONFIGURATION"
    print_info "Output Directory: $OUTPUT_DIR"
    print_info "Reports Directory: $REPORTS_DIR"
    
    # Check prerequisites
    has_python=false
    if check_prerequisites; then
        has_python=true
        
        # Install Python dependencies if available
        if ! install_python_dependencies; then
            has_python=false
        fi
    fi
    
    # Clean directories
    clean_directories
    
    # Build solution
    if ! build_solution; then
        print_error "Build failed! Exiting..."
        exit 1
    fi
    
    # Run tests
    if ! run_tests; then
        print_error "Test execution failed! Exiting..."
        exit 1
    fi
    
    # Generate coverage report
    has_coverage=false
    if generate_coverage_report; then
        has_coverage=true
    fi
    
    # Generate Excel report
    excel_file=""
    if [ "$has_python" = true ]; then
        excel_file=$(generate_excel_report $has_python)
    fi
    
    # Show summary
    show_summary "$excel_file" $has_coverage
    
    # Open reports
    open_reports "$excel_file" $has_coverage
    
    echo
    print_success "🎉 Test execution completed!"
}

# Run main function
main "$@"