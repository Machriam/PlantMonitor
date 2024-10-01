#!/bin/bash

if [ "$#" -ne 4 ]; then
    echo "Usage: $0 input_video output_gif fps lossyness"
    echo "Example: $0 input.mp4 output.gif 10 80"
    exit 1
fi

ffmpeg -y -i "$1" -filter_complex \
"fps=$3,scale=-1:-1:flags=lanczos,split[v1][v2];[v1]palettegen=stats_mode=full[palette];[v2][palette]paletteuse=dither=sierra2_4a" \
"$2"
echo "Optimizing gif..."
gifsicle -O3 --lossy="$4" -o "$2" "$2"