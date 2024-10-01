#!/bin/bash

if [ "$#" -ne 3 ]; then
    echo "Usage: $0 input_video output_gif fps"
    echo "Example: $0 input.mp4 output.gif 10"
    exit 1
fi

ffmpeg -y -i "$1" -filter_complex \
"fps=$3,scale=-1:-1:flags=lanczos,split[v1][v2];[v1]palettegen=stats_mode=full[palette];[v2][palette]paletteuse=dither=sierra2_4a" \
"$2"