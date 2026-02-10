#!/bin/bash
# Run Java-C# Interoperability Tests (simplified)
# For the full test suite, use run-java-interop-tests.sh instead.
# This script generates Java test files and runs C# validation tests.

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
INTEROP_TEST_DIR="${INTEROP_TEST_DIR:-/tmp/interop-tests}"
JAVA_COMPREHENSIVE_DIR="$INTEROP_TEST_DIR/java-comprehensive"

echo "========================================="
echo "TSFile Interoperability Test Suite"
echo "========================================="
echo ""

# Step 1: Build Java
echo "Step 1: Building Java TSFile and test generator..."
cd "$SCRIPT_DIR/java"
# Compile tsfile module (produces full classes in target/classes)
# Note: 'install' phase triggers OSGi bundle plugin which strips classes from jar,
# so we compile first, then manually install the full jar.
mvn clean compile -pl tsfile -am -Dmaven.test.skip=true -Dmaven.javadoc.skip=true -Dmdep.analyze.skip=true -Drat.skip=true -q
# Create full jar from compiled classes and install to local repo
jar cf "$SCRIPT_DIR/java/tsfile/target/tsfile-2.2.1-SNAPSHOT.jar" \
    -C "$SCRIPT_DIR/java/tsfile/target/classes" .
mvn install:install-file \
    -Dfile="$SCRIPT_DIR/java/tsfile/target/tsfile-2.2.1-SNAPSHOT.jar" \
    -DgroupId=org.apache.tsfile -DartifactId=tsfile -Dversion=2.2.1-SNAPSHOT \
    -Dpackaging=jar -q
cd "$SCRIPT_DIR/java/interop-tests"
mvn clean package -Dmaven.test.skip=true -q
echo ""

# Step 2: Generate comprehensive test files
echo "Step 2: Generating comprehensive test files..."
mkdir -p "$JAVA_COMPREHENSIVE_DIR"
mvn exec:java@generate-comprehensive-files \
    -Dexec.args="$JAVA_COMPREHENSIVE_DIR" \
    -q

FILE_COUNT=$(find "$JAVA_COMPREHENSIVE_DIR" -name "*.tsfile" 2>/dev/null | wc -l)
echo "Generated $FILE_COUNT test files in $JAVA_COMPREHENSIVE_DIR"
echo ""

# Step 3: Run C# interop tests
echo "Step 3: Running C# interoperability tests..."
cd "$SCRIPT_DIR"
export JAVA_COMPREHENSIVE_DIR="$JAVA_COMPREHENSIVE_DIR"
dotnet test csharp/tests/Apache.TsFile.Tests/Apache.TsFile.Tests.csproj \
    --configuration Release \
    --filter "FullyQualifiedName~TsFileV4InteropTests" \
    --logger "console;verbosity=normal"

echo ""
echo "========================================="
echo "Test run complete!"
echo "========================================="
