#!/usr/bin/env python3
"""
Helper utilities cho việc parse và analyze test results
"""

import xml.etree.ElementTree as ET
import json
import re
from typing import Dict, List, Optional, Tuple
from dataclasses import dataclass
from datetime import datetime

@dataclass
class TestCase:
    """Data class representing một test case"""
    id: str
    name: str
    class_name: str
    method_name: str
    outcome: str
    duration: str
    start_time: str
    end_time: str
    error_message: str = ""
    stack_trace: str = ""

@dataclass
class TestSuite:
    """Data class representing một test suite"""
    name: str
    total_tests: int
    passed: int
    failed: int
    skipped: int
    duration: str
    test_cases: List[TestCase]

class TestResultAnalyzer:
    """Class để analyze và extract insights từ test results"""
    
    def __init__(self):
        self.test_suites: List[TestSuite] = []
        self.total_tests = 0
        self.total_passed = 0
        self.total_failed = 0
        self.total_skipped = 0
    
    def analyze_test_patterns(self, test_cases: List[TestCase]) -> Dict:
        """Analyze test patterns và tìm common issues"""
        patterns = {
            'authentication_tests': 0,
            'database_tests': 0,
            'api_tests': 0,
            'service_tests': 0,
            'validation_tests': 0,
            'integration_tests': 0,
            'unit_tests': 0
        }
        
        error_patterns = {
            'null_reference': 0,
            'timeout': 0,
            'database_connection': 0,
            'authentication': 0,
            'validation': 0,
            'network': 0
        }
        
        for test in test_cases:
            # Analyze test types based on name patterns
            test_name_lower = test.name.lower()
            class_name_lower = test.class_name.lower()
            
            if 'auth' in test_name_lower or 'login' in test_name_lower:
                patterns['authentication_tests'] += 1
            elif 'database' in test_name_lower or 'db' in test_name_lower or 'repository' in class_name_lower:
                patterns['database_tests'] += 1
            elif 'api' in test_name_lower or 'controller' in class_name_lower:
                patterns['api_tests'] += 1
            elif 'service' in class_name_lower:
                patterns['service_tests'] += 1
            elif 'valid' in test_name_lower:
                patterns['validation_tests'] += 1
            elif 'integration' in test_name_lower or 'integration' in class_name_lower:
                patterns['integration_tests'] += 1
            else:
                patterns['unit_tests'] += 1
            
            # Analyze error patterns for failed tests
            if test.outcome == 'Failed' and test.error_message:
                error_lower = test.error_message.lower()
                if 'null' in error_lower and 'reference' in error_lower:
                    error_patterns['null_reference'] += 1
                elif 'timeout' in error_lower:
                    error_patterns['timeout'] += 1
                elif 'database' in error_lower or 'connection' in error_lower:
                    error_patterns['database_connection'] += 1
                elif 'unauthorized' in error_lower or 'forbidden' in error_lower:
                    error_patterns['authentication'] += 1
                elif 'validation' in error_lower or 'invalid' in error_lower:
                    error_patterns['validation'] += 1
                elif 'network' in error_lower or 'http' in error_lower:
                    error_patterns['network'] += 1
        
        return {
            'test_type_distribution': patterns,
            'error_pattern_analysis': error_patterns,
            'failure_rate_by_type': self._calculate_failure_rates(test_cases, patterns)
        }
    
    def _calculate_failure_rates(self, test_cases: List[TestCase], patterns: Dict) -> Dict:
        """Calculate failure rates cho từng loại test"""
        failure_rates = {}
        
        for pattern_name in patterns.keys():
            if patterns[pattern_name] > 0:
                failed_count = 0
                total_count = patterns[pattern_name]
                
                for test in test_cases:
                    if self._test_matches_pattern(test, pattern_name) and test.outcome == 'Failed':
                        failed_count += 1
                
                failure_rates[pattern_name] = {
                    'total': total_count,
                    'failed': failed_count,
                    'rate': (failed_count / total_count * 100) if total_count > 0 else 0
                }
        
        return failure_rates
    
    def _test_matches_pattern(self, test: TestCase, pattern: str) -> bool:
        """Check xem test có match với pattern không"""
        test_name_lower = test.name.lower()
        class_name_lower = test.class_name.lower()
        
        pattern_mapping = {
            'authentication_tests': lambda: 'auth' in test_name_lower or 'login' in test_name_lower,
            'database_tests': lambda: 'database' in test_name_lower or 'db' in test_name_lower or 'repository' in class_name_lower,
            'api_tests': lambda: 'api' in test_name_lower or 'controller' in class_name_lower,
            'service_tests': lambda: 'service' in class_name_lower,
            'validation_tests': lambda: 'valid' in test_name_lower,
            'integration_tests': lambda: 'integration' in test_name_lower or 'integration' in class_name_lower,
            'unit_tests': lambda: True  # Default case
        }
        
        return pattern_mapping.get(pattern, lambda: False)()
    
    def generate_recommendations(self, analysis_results: Dict) -> List[str]:
        """Generate recommendations dựa trên test analysis"""
        recommendations = []
        
        error_patterns = analysis_results['error_pattern_analysis']
        failure_rates = analysis_results['failure_rate_by_type']
        
        # Recommendations based on error patterns
        if error_patterns['null_reference'] > 0:
            recommendations.append(
                f"🔍 Có {error_patterns['null_reference']} lỗi null reference. "
                "Khuyến nghị: Thêm null checks và validation."
            )
        
        if error_patterns['database_connection'] > 0:
            recommendations.append(
                f"🗄️ Có {error_patterns['database_connection']} lỗi database connection. "
                "Khuyến nghị: Kiểm tra connection string và database availability."
            )
        
        if error_patterns['timeout'] > 0:
            recommendations.append(
                f"⏱️ Có {error_patterns['timeout']} lỗi timeout. "
                "Khuyến nghị: Tối ưu performance hoặc tăng timeout configuration."
            )
        
        # Recommendations based on failure rates
        for test_type, rates in failure_rates.items():
            if rates['rate'] > 50:  # >50% failure rate
                recommendations.append(
                    f"⚠️ {test_type} có failure rate cao ({rates['rate']:.1f}%). "
                    f"Cần review {rates['failed']}/{rates['total']} test cases."
                )
        
        if not recommendations:
            recommendations.append("✅ Tất cả test cases đều ổn định. Tiếp tục maintain code quality!")
        
        return recommendations

def extract_test_metrics(trx_file_path: str) -> Dict:
    """Extract detailed metrics từ TRX file"""
    tree = ET.parse(trx_file_path)
    root = tree.getroot()
    
    namespaces = {'ns': 'http://microsoft.com/schemas/VisualStudio/TeamTest/2010'}
    
    metrics = {
        'total_duration': '00:00:00',
        'slowest_tests': [],
        'fastest_tests': [],
        'test_distribution': {},
        'execution_timeline': []
    }
    
    test_times = []
    
    for result in root.findall('.//ns:UnitTestResult', namespaces):
        test_name = result.get('testName', '')
        duration = result.get('duration', '00:00:00')
        start_time = result.get('startTime', '')
        outcome = result.get('outcome', '')
        
        # Parse duration to seconds for analysis
        duration_seconds = parse_duration_to_seconds(duration)
        
        test_times.append({
            'name': test_name,
            'duration': duration,
            'duration_seconds': duration_seconds,
            'start_time': start_time,
            'outcome': outcome
        })
    
    # Sort by duration để tìm slowest/fastest
    test_times.sort(key=lambda x: x['duration_seconds'], reverse=True)
    
    metrics['slowest_tests'] = test_times[:5]  # Top 5 slowest
    metrics['fastest_tests'] = test_times[-5:]  # Top 5 fastest
    
    # Calculate total duration
    total_seconds = sum(t['duration_seconds'] for t in test_times)
    metrics['total_duration'] = format_seconds_to_duration(total_seconds)
    
    return metrics

def parse_duration_to_seconds(duration_str: str) -> float:
    """Parse duration string (HH:MM:SS.mmm) to seconds"""
    try:
        if not duration_str or duration_str == '00:00:00':
            return 0.0
        
        # Handle format: HH:MM:SS.mmm
        time_part = duration_str.split('.')[0]
        milliseconds = 0
        
        if '.' in duration_str:
            milliseconds = int(duration_str.split('.')[1][:3])  # Take first 3 digits
        
        h, m, s = map(int, time_part.split(':'))
        total_seconds = h * 3600 + m * 60 + s + milliseconds / 1000.0
        
        return total_seconds
    except:
        return 0.0

def format_seconds_to_duration(seconds: float) -> str:
    """Format seconds back to HH:MM:SS.mmm format"""
    hours = int(seconds // 3600)
    minutes = int((seconds % 3600) // 60)
    secs = int(seconds % 60)
    milliseconds = int((seconds % 1) * 1000)
    
    return f"{hours:02d}:{minutes:02d}:{secs:02d}.{milliseconds:03d}"

if __name__ == "__main__":
    # Example usage
    analyzer = TestResultAnalyzer()
    
    # Test with sample data
    sample_tests = [
        TestCase("TC001", "AuthService.LoginTest", "AuthService", "LoginTest", "Passed", "00:00:01.234", "", ""),
        TestCase("TC002", "UserRepository.GetUserTest", "UserRepository", "GetUserTest", "Failed", "00:00:02.456", "", "", "Null reference exception"),
    ]
    
    analysis = analyzer.analyze_test_patterns(sample_tests)
    recommendations = analyzer.generate_recommendations(analysis)
    
    print("Analysis Results:", json.dumps(analysis, indent=2))
    print("\nRecommendations:")
    for rec in recommendations:
        print(f"- {rec}")