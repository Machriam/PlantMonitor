﻿namespace PlantMonitorControl.Features.HealthChecking;

public class PlantList
{
    public string GetRandomPlant()
    {
        var plantList = Plants.Split("\n").Select(p => p.Trim()).Where(p => !p.IsEmpty()).ToList();
        return plantList[Random.Shared.Next(0, plantList.Count)];
    }

    private const string Plants = """
        alder
        african rice
        african violet
        algerian oak quercus
        almond
        ambrosia
        american cress
        american dogwood
        american nightshade
        american white hellebore
        american winterberry
        amy root
        annual sow thistle
        appalachian tea
        apple
        apricot
        arfaj
        arizona sycamore
        arrow wood
        ash
        ash leaved maple
        asian rice
        azolla
        baby rose
        bamboo
        banana
        bank cress
        baobab
        bastard pellitory
        bay
        bay laurel
        bean
        bear corn
        bearberry
        beech
        belle isle cress
        bermuda cress
        betula lenta
        big hellebore
        bindweed
        birch
        birds nest
        birds nest plant
        bitter nightshade
        bitter weed
        bittercress
        bittersweet
        black alder
        black ash
        black birch
        black cap
        black cherry
        black eyed susan
        black hellebore
        black maple
        black nightshade
        black raspberry
        black weed
        blackberry
        blackhaw
        blackhaw viburnum
        blackie head
        blue ash
        blue bindweed
        blue oak
        blue of the heavens
        blueberry
        blueberry cornel
        blunt leaved milkweed
        bolean birch
        boston fern or sword fern
        bow wood
        box
        boxelder
        boxwood
        brier
        brilliant coneflower
        bristly dewberry
        bristly ground berry
        brittle bush
        broadleaf
        broadleaf plantain
        brown betty
        brown daisy
        brown eyed susan
        buckeye
        buckeye california buckeye
        buffalo weed
        bulbous cress
        bull nettle
        bur oak
        butterfly flower
        butterfly weed
        cabbage
        cabinet cherry
        california bay
        california black oak
        california buckeye
        california sycamore
        california thistle
        california walnut
        canada root
        canada thistle
        cancer jalap
        cane ash
        canoe birch
        canyon live oak
        carolina azolla
        carolina horse nettle
        carrot
        carrot weed
        cart track plant
        catalina ironwood
        champion oak
        cherry
        cherry birch
        chestnut
        chigger flower
        christmas fern
        chrysanthemum
        climbing nightshade
        clove
        clover
        clump foot cabbage
        coakum
        coast live oak
        coast polypody
        coconut
        coffee plant
        colic weed
        collard
        colwort
        common alder
        common daisy
        common fig
        common milkweed
        common onion
        common plantain
        common ragweed
        common ragwort
        common serviceberry
        common tansy
        common yarrow
        coneflower
        cork oak
        corn sow thistle
        corn speedwell
        corn thistle
        cornel
        cornelian tree
        corydalis
        cotton plant
        coyote willow
        creek maple
        creeping thistle
        creeping yellow cress
        cress
        crowfoot
        crows nest
        crows toes
        cucumber
        cursed thistle
        cutleaf coneflower
        cutleaf maple
        cutleaf toothwort
        daisy
        damask violet
        dames gilli flower
        dames rocket
        dames violet
        deadly nightshade
        deadnettle
        deciduous holly
        devils bite
        devils darning needle
        devils nose
        devils plague
        dewberry
        dindle
        dogtooth violet
        dogwood
        dooryard plantain
        downy serviceberry
        drumstick
        duck retten
        duscle
        dwarf wild rose
        dye leaves
        dyers oak
        early winter cress
        earth gall
        eastern black oak
        eastern coneflower
        eastern redbud
        english bulls eye
        english oak
        eucalyptus
        european flax
        european holly
        european pellitory
        european weeping birch
        european white birch
        european white hellebore
        evergreen huckleberry
        evergreen winterberry
        extinguisher moss
        eytelia
        fair maid of france
        fairymoss azolla caroliniana
        false alder
        false box
        false boxwood
        false hellebore
        fellenwort
        felonwood
        felonwort
        fennel
        fern leaf corydalis
        fern leaf yarrow
        ferns
        fever bush
        feverfew
        field sow thistle
        fig
        flax
        florida dogwood
        flowering dogwood
        fluxroot
        fumewort
        gallberry
        garden nightshade
        garget
        garlic
        garlic mustard
        garlic root
        gewa bangladesh
        giant onion
        giant ragweed
        gilli flower
        gloriosa daisy
        golden buttons
        golden corydalis
        golden garlic
        golden jerusalem
        goldenglow
        goodding willow
        goose tongue
        gordaldo
        grapefruit
        grapevine
        gray alder
        gray birch
        great ragweed
        greater plantain
        green ash
        green headed coneflower
        green thistle
        ground berry
        gutweed
        hairy bittercress
        haldi
        hard thistle
        hares colwort
        hares thistle
        harlequin
        hay fever weed
        healing blade
        hedge plant
        hellebore
        hemp
        hemp dogbane
        hen plant
        henbit deadnettle
        herb barbara
        hispid swamp blackberry
        hoary ragwort
        hogweed
        holly
        honey mesquite
        horse cane
        horse nettle
        horsetail milkweed
        hounds berry
        houseleek
        huckleberry
        indian arrow wood
        indian hemp
        indian paintbrush
        indian poke
        indian posy
        inkberry
        inkberry holly
        island oak
        isle of man cabbage
        itch weed
        ivy
        jack by the hedge
        jack in the bush
        japanese flowering dogwood
        judas tree
        juneberry
        juniper
        keek
        kimberly queen fern
        kinnikinnik
        korean rock fern
        kousa
        kousa dogwood
        kudzu
        lace flower
        lambs cress
        lambs foot
        land cress
        lavender
        leatherleaf viburnum
        leek
        lemon
        lettuce
        lilac
        lily leek
        love vine
        low rose
        mahogany birch
        maize
        mango
        maple
        maple ash
        marina
        marsh ragwort
        meadow cabbage
        meadow holly
        mesquite
        milfoil
        milk thistle
        milkweed
        milky tassel
        mirbecks oak
        moose maple
        moosewood
        morel
        mosquito fern
        mosquito plant
        mossycup white oak
        mother of the evening
        mountain mahogany
        mulberry
        multiflora rose
        neem
        nettle
        new zealand flax
        night scented gilli flower
        nightshade
        nodding onion
        nodding thistle
        northern red oak
        nosebleed
        oak tree quercus
        olive
        onion
        orange
        orange coneflower
        orange milkweed
        orange root
        orange swallow wort
        osage
        osage orange
        osier salix
        oxford ragwort
        pacific dogwood
        pale corydalis
        paper birch
        parsley
        parsnip
        pea
        peach
        peanut
        pear
        pedunculate oak
        pellitory
        pennsylvania blackberry
        penny hedge
        pepper root
        perennial thistle
        petty morel
        pigeon berry
        pin oak
        pine
        pineapple
        pink corydalis
        pistachio
        plane european sycamore
        plantain
        pleurisy root
        pocan bush
        poison ivy
        poison berry
        poison flower
        poke
        pokeroot
        pokeweed
        polecat weed
        polkweed
        poor annie
        poor mans mustard
        poorland daisy
        poplar
        poppy
        possumhaw
        potato
        prairie rose
        prickly thistle
        pudina
        purple flowered toothwort
        purple raspberry
        queen annes lace
        queens gilli flower
        quercitron
        radical weed
        ragweed
        ragwort
        rambler rose
        rantipole
        rapeseed
        raspberry
        red ash
        red birch
        red deadnettle
        red ink plant
        red mulberry
        red oak
        red osier
        red river maple
        red willow
        red brush
        redbud
        red weed
        redwood sorrel
        rheumatism root
        rhubarb
        ribwort
        rice
        river ash
        river birch
        river maple
        road weed
        rock harlequin
        rocket
        rocket cress
        rogues gilli flower
        roman wormwood
        rose
        rose milkweed
        rose willow
        rosemary
        round leaf plantain
        rum cherry
        running swamp blackberry
        rye
        saffron crocus
        sand brier
        sanguinary
        saskatoon
        sauce alone
        scarlet berry
        scarlet oak
        scoke
        scotch cap
        scrambled eggs
        screw bean mesquite
        scrub oak
        scurvy cress
        scurvy grass
        serviceberry
        sessile oak
        shad blow
        shad blow serviceberry
        shadbush
        sharp fringed sow thistle
        silkweed
        silky cornel
        silky dogwood
        silky swallow wort
        silver birch
        silver maple
        silver ragwort
        silver leaf maple
        skunk cabbage
        skunk weed
        small flowered thistle
        snake berry
        sneezeweed
        sneezewort
        sneezewort yarrow
        snowdrop
        soft maple
        soldiers woundwort
        sorrel
        spanish oak
        speckled alder
        speedwell
        spice birch
        spiny leaved sow thistle
        spiny sow thistle
        spool wood
        spotted deadnettle
        spotted oak
        spring cress
        squaw bush
        stag bush
        stammerwort
        star of persia
        stickweed
        strawberry
        strawberry tree
        striped alder
        striped maple
        sugar maple
        sugarcane
        sugarplum
        summer lilac
        sundari bangladesh
        sunflower
        swallow wort
        swamp ash
        swamp cabbage
        swamp dewberry
        swamp dogwood
        swamp hellebore
        swamp holly
        swamp maple
        swamp milkweed
        swamp oak
        swamp silkweed
        swamp spanish oak
        swamp white oak
        sweet birch
        sweet orange
        sweet potato
        sweet potato vine
        sweet rocket
        swine thistle
        swinies
        sword ferns
        sycamore
        sycamore american
        sycamore arizona
        sycamore california
        tall ambrosia
        tall coneflower
        tansy
        tassel weed
        tea
        thimbleberry
        thimbleweed
        thin leaved coneflower
        thistle
        thousand leaf
        thousand seal
        three leaved coneflower
        thyme
        tickle weed
        tobacco plant
        tomato
        toothwort
        touch me not
        trailing bittersweet
        trailing nightshade
        trailing red huckleberry
        trailing violet nightshade
        travellers joy
        tread softly
        tree onion
        tree sow thistle
        tree tobacco
        trillium
        true cinnamon
        tuber root
        tulip
        tulsi
        upland cress
        valley oak
        vanilla orchid
        viburnum
        viola species
        violet
        violet bloom
        virginia silk weed
        virginia virgins bower
        virginia winterberry
        virgins bower
        wall speedwell
        walnut
        walnut california walnut
        water ash
        water birch
        water fern
        water maple
        way thistle
        way bread
        wayside plantain
        weeping birch
        western redbud
        western sword fern
        western trillium
        western wake robin
        wheat
        whiskey cherry
        white alder
        white ash
        white birch
        white cornel
        white hellebore
        white indian hemp
        white mans foot
        white maple
        white mulberry
        white oak
        white root
        white tansy
        white trillium
        whorled milkweed
        wild black cherry
        wild carrot
        wild cherry
        wild cotton
        wild garlic
        wild hops
        wild onion
        wild orange
        wild pellitory
        wild rose
        wild tansy
        willow
        wind root
        wineberry
        winter gilli flower
        winter rocket
        winterberry
        winterberry holly
        winter cress
        woodbine
        woody nightshade
        woolly yarrow
        wormwood
        wound rocket
        yam dios corea
        yarrow
        yellow bark oak
        yellow birch
        yellow coneflower
        yellow corydalis
        yellow daisy
        yellow field cress
        yellow fume wort
        yellow harlequin
        yellow milkweed
        yellow ox eye daisy
        yellow rocket
        yellow wood
        zedoary
        """;
}
