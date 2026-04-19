#!/bin/nsh
# scripting-demo.sh — Demonstrates advanced nsh shell scripting features
#
# Usage: scripting-demo.sh
#
# Creates a demo directory and walks through all shell script features:
#   - Variables and positional parameters
#   - Command substitution $()
#   - if/elif/else/fi conditionals
#   - for loops
#   - while loops
#   - case statements
#   - Pipes and grep
#   - break/continue
#   - File tests (-f, -d, -e, -z, -n)
#   - Numeric comparisons (-eq, -lt, -gt)
#   - String comparisons (=, !=)

DEMO_DIR=/tmp/script-demo
DIVIDER="????????????????????????????????????????"

echo ""
echo "??????????????????????????????????????????????"
echo "?   nsh Advanced Shell Scripting Demo        ?"
echo "??????????????????????????????????????????????"
echo ""

# ?? Setup ?????????????????????????????????????????????
echo "$DIVIDER"
echo "1. SETUP — Creating demo directory and files"
echo "$DIVIDER"

if [ -d $DEMO_DIR ]; then
    echo "  Cleaning up previous demo..."
    rm -r $DEMO_DIR
fi

mkdir -p $DEMO_DIR
echo "  Created $DEMO_DIR"

# Create test files
echo "Hello World" > $DEMO_DIR/hello.txt
echo "This is a test file" > $DEMO_DIR/test.txt
echo "Error: something went wrong" > $DEMO_DIR/error.log
echo "Warning: disk space low" > $DEMO_DIR/warning.log
echo "Error: connection failed" > $DEMO_DIR/app.log
echo "Info: server started" >> $DEMO_DIR/app.log
echo "Error: timeout exceeded" >> $DEMO_DIR/app.log
echo "Info: request processed" >> $DEMO_DIR/app.log

echo "#!/bin/nsh" > $DEMO_DIR/myscript.sh
echo "echo hello from myscript" >> $DEMO_DIR/myscript.sh

echo "first,Alice,30" > $DEMO_DIR/data.csv
echo "second,Bob,25" >> $DEMO_DIR/data.csv
echo "third,Carol,35" >> $DEMO_DIR/data.csv

echo "  Created test files: hello.txt, test.txt, error.log, warning.log,"
echo "    app.log, myscript.sh, data.csv"
echo ""

# ?? Variables ?????????????????????????????????????????
echo "$DIVIDER"
echo "2. VARIABLES — Assignment and expansion"
echo "$DIVIDER"

NAME="NetNIX"
VERSION=2
echo "  NAME=$NAME"
echo "  VERSION=$VERSION"
echo "  Built-in vars: USER=$USER, HOME=$HOME, CWD=$CWD"
echo ""

# ?? Command Substitution ?????????????????????????????
echo "$DIVIDER"
echo "3. COMMAND SUBSTITUTION — \$(command)"
echo "$DIVIDER"

FILE_COUNT=$(ls $DEMO_DIR | wc -l)
echo "  Files in demo dir: $FILE_COUNT"

HELLO_CONTENT=$(cat $DEMO_DIR/hello.txt)
echo "  Content of hello.txt: $HELLO_CONTENT"
echo ""

# ?? If/Elif/Else ?????????????????????????????????????
echo "$DIVIDER"
echo "4. IF/ELIF/ELSE — Conditional execution"
echo "$DIVIDER"

echo "  Test: does hello.txt exist?"
if [ -f $DEMO_DIR/hello.txt ]; then
    echo "    YES — hello.txt exists"
else
    echo "    NO — hello.txt not found"
fi

echo "  Test: does /nonexistent exist?"
if [ -e /nonexistent ]; then
    echo "    YES — found it"
else
    echo "    NO — does not exist (correct!)"
fi

echo "  Test: is DEMO_DIR a directory?"
if [ -d $DEMO_DIR ]; then
    echo "    YES — $DEMO_DIR is a directory"
fi

echo "  Test: numeric comparison (VERSION=2)"
if [ $VERSION -lt 1 ]; then
    echo "    VERSION < 1"
elif [ $VERSION -eq 2 ]; then
    echo "    VERSION equals 2 (correct!)"
else
    echo "    VERSION > 2"
fi

echo "  Test: string comparison"
if [ $NAME = "NetNIX" ]; then
    echo "    NAME equals 'NetNIX' (correct!)"
fi

echo "  Test: empty string check"
EMPTY=""
if [ -z "$EMPTY" ]; then
    echo "    EMPTY is zero-length (correct!)"
fi

if [ -n "$NAME" ]; then
    echo "    NAME is non-empty (correct!)"
fi
echo ""

# ?? For Loops ?????????????????????????????????????????
echo "$DIVIDER"
echo "5. FOR LOOPS — Iterating over lists"
echo "$DIVIDER"

echo "  Iterating over colors:"
for COLOR in red green blue yellow; do
    echo "    - $COLOR"
done

echo "  Iterating over files in demo dir:"
for FILE in $(ls $DEMO_DIR); do
    echo "    - $FILE"
done
echo ""

# ?? While Loops ???????????????????????????????????????
echo "$DIVIDER"
echo "6. WHILE LOOPS — Conditional iteration"
echo "$DIVIDER"

COUNTER=1
echo "  Counting to 5:"
while [ $COUNTER -le 5 ]; do
    echo "    count = $COUNTER"
    COUNTER=$(($COUNTER + 1))
done
echo ""

# ?? Break and Continue ????????????????????????????????
echo "$DIVIDER"
echo "7. BREAK AND CONTINUE"
echo "$DIVIDER"

echo "  For loop with break at 3:"
for NUM in 1 2 3 4 5; do
    if [ $NUM -eq 3 ]; then
        echo "    $NUM — breaking!"
        break
    fi
    echo "    $NUM"
done

echo "  For loop skipping 'green' with continue:"
for COLOR in red green blue; do
    if [ $COLOR = "green" ]; then
        continue
    fi
    echo "    - $COLOR"
done
echo ""

# ?? Case Statements ???????????????????????????????????
echo "$DIVIDER"
echo "8. CASE STATEMENTS — Pattern matching"
echo "$DIVIDER"

ANIMAL="cat"
echo "  ANIMAL=$ANIMAL"
case $ANIMAL in
    dog) echo "    It's a dog!" ;;
    cat) echo "    It's a cat! (matched!)" ;;
    *) echo "    Unknown animal" ;;
esac

OS="netnix"
echo "  OS=$OS"
case $OS in
    linux) echo "    Running Linux" ;;
    netnix) echo "    Running NetNIX! (matched!)" ;;
    *) echo "    Unknown OS" ;;
esac
echo ""

# ?? Pipes and Grep ????????????????????????????????????
echo "$DIVIDER"
echo "9. PIPES AND GREP — Filtering output"
echo "$DIVIDER"

echo "  Errors in app.log (grep 'Error'):"
cat $DEMO_DIR/app.log | grep "Error"

echo ""
echo "  All .log files (ls | grep):"
ls $DEMO_DIR | grep ".log"

echo ""
echo "  Line count of app.log (cat | wc):"
LINES=$(cat $DEMO_DIR/app.log | wc -l)
echo "    $LINES lines"

echo ""
echo "  Inverted grep — lines WITHOUT 'Error':"
cat $DEMO_DIR/app.log | grep -v "Error"

echo ""
echo "  Case-insensitive grep for 'error' in app.log:"
grep -i "error" $DEMO_DIR/app.log

echo ""
echo "  Grep with line numbers:"
grep -n "Error" $DEMO_DIR/app.log

echo ""
echo "  Pipe to file — saving errors to errors.txt:"
cat $DEMO_DIR/app.log | grep "Error" > $DEMO_DIR/errors.txt
echo "    Saved. Contents:"
cat $DEMO_DIR/errors.txt
echo ""

# ?? Combining Features ???????????????????????????????
echo "$DIVIDER"
echo "10. COMBINING FEATURES — Real-world patterns"
echo "$DIVIDER"

echo "  Checking all files for errors:"
for FILE in $(ls $DEMO_DIR); do
    FULL=$DEMO_DIR/$FILE
    if [ -f $FULL ]; then
        ERRORS=$(grep -c "Error" $FULL)
        if [ $ERRORS -gt 0 ]; then
            echo "    $FILE: $ERRORS error(s) found"
        fi
    fi
done

echo ""
echo "  Building a report:"
TOTAL_FILES=$(ls $DEMO_DIR | wc -l)
TOTAL_ERRORS=$(grep -c "Error" $DEMO_DIR/app.log)
echo "    Total files: $TOTAL_FILES"
echo "    Total errors in app.log: $TOTAL_ERRORS"
echo ""

# ?? Cleanup ???????????????????????????????????????????
echo "$DIVIDER"
echo "DONE — Demo complete!"
echo "$DIVIDER"
echo ""
echo "Demo files are in $DEMO_DIR"
echo "To clean up: rm -r $DEMO_DIR"
echo ""
