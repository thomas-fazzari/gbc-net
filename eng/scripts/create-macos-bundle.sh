#!/usr/bin/env sh
set -eu

app_project=${1:?Usage: create-macos-bundle.sh <app-project> <runtime>}
runtime=${2:?Usage: create-macos-bundle.sh <app-project> <runtime>}
dotnet=${DOTNET:-dotnet}

repo_root=$(CDPATH='' cd -- "$(dirname -- "$0")/../.." && pwd)
packaging_dir=$repo_root/eng/packaging/macos

case "$app_project" in
/*) ;;
*) app_project=$repo_root/$app_project ;;
esac

case "$runtime" in
osx-*) ;;
*)
        echo "bundle requires RUNTIME=osx-*." >&2
        exit 1
        ;;
esac

project_dir=$(CDPATH='' cd -- "$(dirname -- "$app_project")" && pwd)
escape_sed_replacement() {
        printf '%s' "$1" | sed 's/[&|]/\\&/g'
}

target_framework=$("$dotnet" msbuild "$app_project" -getProperty:TargetFramework -nologo)
executable=$("$dotnet" msbuild "$app_project" -getProperty:AssemblyName -nologo)
display_name=$("$dotnet" msbuild "$app_project" -getProperty:Product -nologo)
version=$("$dotnet" msbuild "$app_project" -getProperty:Version -nologo)

if [ -z "$display_name" ]; then
        display_name=$executable
fi

if [ -z "$version" ]; then
        version=1.0.0
fi

publish_root="$project_dir/bin/Release/$target_framework/$runtime"
publish_dir="$publish_root/publish"
bundle_dir="$publish_root/$display_name.app"

"$dotnet" restore "$app_project" --runtime "$runtime" -p:SelfContained=true
rm -rf "$publish_dir"
"$dotnet" publish "$app_project" --configuration Release --runtime "$runtime" --self-contained true --no-restore

rm -rf "$bundle_dir"
mkdir -p "$bundle_dir/Contents/MacOS" "$bundle_dir/Contents/Resources"
rsync -a --delete --exclude '*.dSYM' --exclude '*.pdb' --exclude '.DS_Store' "$publish_dir/" "$bundle_dir/Contents/MacOS/"
cp "$packaging_dir/GbcNet.icns" "$bundle_dir/Contents/Resources/GbcNet.icns"
sed \
        -e "s|__EXECUTABLE__|$(escape_sed_replacement "$executable")|g" \
        -e "s|__DISPLAY_NAME__|$(escape_sed_replacement "$display_name")|g" \
        -e "s|__VERSION__|$(escape_sed_replacement "$version")|g" \
        "$packaging_dir/Info.plist.in" >"$bundle_dir/Contents/Info.plist"
chmod +x "$bundle_dir/Contents/MacOS/$executable"
touch "$bundle_dir"

echo "Created $bundle_dir"
