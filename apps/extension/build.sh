#!/bin/bash

# ============================================
# Build Script for AI Cover Letter Extension
# ============================================

# Exit on error
set -e

echo "🚀 Starting build..."

# 1. Clean dist folder
echo "🧹 Cleaning dist folder..."
rm -rf dist
mkdir -p dist/popup dist/background dist/content dist/assets dist/utils dist/types

# 2. Compile TypeScript
echo "📦 Compiling TypeScript..."
npx tsc

# 3. Copy static assets
echo "📄 Copying static files..."
cp manifest.json dist/
cp src/popup/index.html dist/popup/
cp src/popup/styles.css dist/popup/
cp assets/*.png dist/assets/

echo "✅ Build complete! Folder 'dist' is ready to be loaded."
