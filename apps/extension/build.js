import fs from 'fs';
import path from 'path';
import { execSync } from 'child_process';

const distDir = 'dist';
const folders = [
  'dist/popup',
  'dist/background',
  'dist/content',
  'dist/assets',
  'dist/utils',
  'dist/types'
];

async function build() {
  console.log('🚀 Starting build...');

  // 1. Clean dist folder
  if (fs.existsSync(distDir)) {
    console.log('🧹 Cleaning dist folder...');
    fs.rmSync(distDir, { recursive: true, force: true });
  }

  // 2. Create folders
  console.log('📁 Creating directories...');
  folders.forEach(dir => {
    fs.mkdirSync(dir, { recursive: true });
  });

  // 3. Install dependencies
  console.log('📥 Installing npm dependencies...');
  try {
    execSync('npm install', { stdio: 'inherit' });
  } catch (error) {
    console.error('❌ npm install failed.');
    process.exit(1);
  }

  // 4. Compile TypeScript
  console.log('📦 Compiling TypeScript...');
  try {
    execSync('npx tsc', { stdio: 'inherit' });
  } catch (error) {
    console.error('❌ TypeScript compilation failed.');
    process.exit(1);
  }

  // 5. Copy static files
  console.log('📄 Copying static files...');
  const filesToCopy = [
    { src: 'manifest.json', dest: 'dist/manifest.json' },
    { src: 'src/popup/index.html', dest: 'dist/popup/index.html' },
    { src: 'src/popup/styles.css', dest: 'dist/popup/styles.css' },
    { src: 'src/popup/onboarding.css', dest: 'dist/popup/onboarding.css' }
  ];

  filesToCopy.forEach(({ src, dest }) => {
    if (fs.existsSync(src)) {
      fs.copyFileSync(src, dest);
    } else {
      console.warn(`⚠️ Warning: ${src} not found.`);
    }
  });

  // 6. Copy icons
  console.log('🖼️ Copying icons...');
  const assetsDir = 'assets';
  if (fs.existsSync(assetsDir)) {
    const icons = fs.readdirSync(assetsDir).filter(f => f.endsWith('.png'));
    icons.forEach(icon => {
      fs.copyFileSync(path.join(assetsDir, icon), path.join('dist/assets', icon));
    });
  } else {
    console.warn('⚠️ Warning: assets folder not found.');
  }

  console.log('✅ Build complete!');
}

build().catch(err => {
  console.error('💥 Build failed:', err);
  process.exit(1);
});
