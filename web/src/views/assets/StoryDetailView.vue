<script setup lang="ts">
import type {
  Story,
  StorySourceLine,
  StoryTypeInfo,
  TranslationLine,
  TranslationVersion,
} from '@/api/assets'
import { BookOpen, FileText, FolderOpen } from 'lucide-vue-next'
import { computed, nextTick, onMounted, ref, watch } from 'vue'
import { RouterLink, useRoute, useRouter } from 'vue-router'
import {
  getStory,
  getStorySourceLines,
  getStoryTypes,
  getTranslationLines,
  getTranslationVersions,
} from '@/api/assets'
import { ApiError } from '@/api/client'
import EmptyState from '@/components/EmptyState.vue'
import ErrorState from '@/components/ErrorState.vue'
import LoadingState from '@/components/LoadingState.vue'
import Card from '@/components/ui/Card.vue'
import CardContent from '@/components/ui/CardContent.vue'
import CardHeader from '@/components/ui/CardHeader.vue'
import CardTitle from '@/components/ui/CardTitle.vue'
import { useAuth } from '@/lib/auth'
import {
  createStoryTypeLabeler,
  formatDateTime,
  formatLineType,
  formatStaff,
  formatTranslationVersion,
} from '@/lib/display'
import { recordRecentStory } from '@/lib/recentAssets'

interface ReaderLine {
  source: StorySourceLine
  translation: TranslationLine | null
}

const route = useRoute()
const router = useRouter()
const { state } = useAuth()
const storyTypes = ref<StoryTypeInfo[]>([])
const story = ref<Story | null>(null)
const sourceLines = ref<StorySourceLine[]>([])
const translationVersions = ref<TranslationVersion[]>([])
const selectedVersionId = ref<number | null>(null)
const translationLines = ref<TranslationLine[]>([])
const loading = ref(true)
const translationLoading = ref(false)
const error = ref<ApiError | null>(null)
const translationError = ref<ApiError | null>(null)
const highlightedLine = ref<number | null>(null)
const storyId = computed(() => Number(route.params.storyId))
const storyTypeLabel = computed(() => createStoryTypeLabeler(storyTypes.value))
const selectedVersion = computed(() => {
  return translationVersions.value.find(version => version.id === selectedVersionId.value) || null
})
const readerLines = computed<ReaderLine[]>(() => {
  const translationsBySource = new Map(translationLines.value.map(line => [line.sourceLineId, line]))
  const translationsByLineNo = new Map(translationLines.value.map(line => [line.lineNo, line]))
  return sourceLines.value.map(source => ({
    source,
    translation: translationsBySource.get(source.id) || translationsByLineNo.get(source.lineNo) || null,
  }))
})

let highlightTimer: ReturnType<typeof setTimeout> | null = null

onMounted(loadPage)
watch(() => route.params.storyId, loadPage)
watch(() => route.query.translation_version_id, selectVersionFromRoute)
watch(() => route.query.line, highlightLineFromRoute)

async function loadPage() {
  loading.value = true
  error.value = null
  translationError.value = null
  translationLines.value = []

  try {
    const [types, detail, lines, versions] = await Promise.all([
      storyTypes.value.length ? Promise.resolve(storyTypes.value) : getStoryTypes(),
      getStory(storyId.value),
      getStorySourceLines(storyId.value),
      getTranslationVersions(storyId.value, { page: 1, pageSize: 100 }),
    ])
    storyTypes.value = types
    story.value = detail
    sourceLines.value = lines
    translationVersions.value = versions.items
    selectedVersionId.value = resolveInitialVersionId()
    recordRecentStory(state.currentTenant?.id, detail)
    await loadTranslationLines()
    await highlightLineFromRoute()
  }
  catch (loadError) {
    error.value = loadError instanceof ApiError
      ? loadError
      : new ApiError(0, '剧情详情加载失败，请稍后重试。')
  }
  finally {
    loading.value = false
  }
}

function resolveInitialVersionId() {
  const requestedId = Number(route.query.translation_version_id)
  if (requestedId && translationVersions.value.some(version => version.id === requestedId)) {
    return requestedId
  }

  return translationVersions.value[0]?.id || null
}

async function selectVersionFromRoute() {
  const requestedId = Number(route.query.translation_version_id)
  if (!requestedId || requestedId === selectedVersionId.value) {
    return
  }

  if (!translationVersions.value.some(version => version.id === requestedId)) {
    return
  }

  selectedVersionId.value = requestedId
  await loadTranslationLines()
}

async function changeVersion(versionId: number) {
  selectedVersionId.value = versionId
  await router.replace({
    path: route.path,
    query: {
      ...route.query,
      translation_version_id: String(versionId),
    },
  })
  await loadTranslationLines()
}

async function loadTranslationLines() {
  translationLines.value = []
  translationError.value = null
  if (!selectedVersionId.value) {
    return
  }

  translationLoading.value = true
  try {
    translationLines.value = await getTranslationLines(selectedVersionId.value)
  }
  catch (loadError) {
    translationError.value = loadError instanceof ApiError
      ? loadError
      : new ApiError(0, '译文加载失败，请稍后重试。')
  }
  finally {
    translationLoading.value = false
  }
}

async function highlightLineFromRoute() {
  const lineNo = Number(route.query.line)
  if (!lineNo) {
    return
  }

  highlightedLine.value = lineNo
  await nextTick()
  document.getElementById(`line-${lineNo}`)?.scrollIntoView({ block: 'center', behavior: 'smooth' })
  if (highlightTimer) {
    clearTimeout(highlightTimer)
  }

  highlightTimer = setTimeout(() => {
    highlightedLine.value = null
  }, 2200)
}

function getLineClass(lineType: string) {
  if (lineType === 'scene' || lineType === 'upper_scene') {
    return 'border-l-4 border-l-amber-500/70 bg-amber-50/60'
  }

  if (lineType === 'choice') {
    return 'border-l-4 border-l-blue-500/70 bg-blue-50/60'
  }

  if (lineType === 'separator') {
    return 'border-l-4 border-l-muted-foreground/40 bg-muted/60'
  }

  return 'border-l-4 border-l-primary/60 bg-card'
}
</script>

<template>
  <div class="space-y-6">
    <div class="flex flex-wrap items-center gap-2 text-sm text-muted-foreground">
      <RouterLink class="hover:text-foreground" to="/assets">
        资产目录
      </RouterLink>
      <span>/</span>
      <RouterLink class="hover:text-foreground" to="/stories?page=1&page_size=20">
        剧情
      </RouterLink>
      <template v-if="story?.group">
        <span>/</span>
        <RouterLink class="hover:text-foreground" :to="`/assets/groups/${story.group.id}`">
          {{ story.group.title }}
        </RouterLink>
      </template>
      <span>/</span>
      <span>{{ story?.title || '剧情详情' }}</span>
    </div>

    <LoadingState v-if="loading" label="加载剧情详情" />
    <ErrorState v-else-if="error" :error="error" @retry="loadPage" />

    <template v-else-if="story">
      <Card>
        <CardHeader>
          <div class="flex flex-wrap items-center gap-2 text-sm text-muted-foreground">
            <BookOpen :size="18" class="text-primary" />
            <span>{{ storyTypeLabel(story.storyType) }}</span>
            <span>·</span>
            <span>{{ story.scenarioId }}</span>
          </div>
          <CardTitle class="text-2xl">
            {{ story.title }}
          </CardTitle>
        </CardHeader>
        <CardContent class="grid gap-4 text-sm md:grid-cols-2 xl:grid-cols-4">
          <div>
            <p class="text-muted-foreground">
              所属剧情集
            </p>
            <RouterLink
              v-if="story.group"
              class="mt-1 inline-flex items-center gap-2 font-medium text-primary hover:underline"
              :to="`/assets/groups/${story.group.id}`"
            >
              <FolderOpen :size="16" />
              {{ story.group.title }}
            </RouterLink>
            <p v-else class="mt-1 font-medium">
              -
            </p>
          </div>
          <div>
            <p class="text-muted-foreground">
              scenario_id
            </p>
            <p class="mt-1 break-all font-medium">
              {{ story.scenarioId }}
            </p>
          </div>
          <div>
            <p class="text-muted-foreground">
              原文行
            </p>
            <p class="mt-1 font-medium">
              {{ sourceLines.length }}
            </p>
          </div>
          <div>
            <p class="text-muted-foreground">
              更新时间
            </p>
            <p class="mt-1 font-medium">
              {{ formatDateTime(story.updatedAt) }}
            </p>
          </div>
        </CardContent>
      </Card>

      <section class="grid gap-4 lg:grid-cols-[280px_1fr]">
        <aside class="space-y-4">
          <Card>
            <CardHeader>
              <CardTitle class="text-base">
                译文版本
              </CardTitle>
            </CardHeader>
            <CardContent>
              <EmptyState
                v-if="translationVersions.length === 0"
                title="暂无译文版本"
                description="还没有为该剧情导入译文。"
              />
              <div v-else class="space-y-2">
                <button
                  v-for="version in translationVersions"
                  :key="version.id"
                  class="w-full rounded-md border p-3 text-left transition hover:border-primary/60"
                  :class="selectedVersionId === version.id ? 'border-primary bg-primary/5' : 'bg-background'"
                  @click="changeVersion(version.id)"
                >
                  <p class="text-sm font-medium">
                    {{ formatTranslationVersion(version) }}
                  </p>
                  <p class="mt-1 text-xs text-muted-foreground">
                    {{ formatStaff(version.staff) }}
                  </p>
                  <p class="mt-2 text-xs text-muted-foreground">
                    创建人 {{ version.createdBy }} · {{ formatDateTime(version.createdAt) }}
                  </p>
                </button>
              </div>
            </CardContent>
          </Card>

          <Card v-if="selectedVersion">
            <CardHeader>
              <CardTitle class="text-base">
                当前译文
              </CardTitle>
            </CardHeader>
            <CardContent class="space-y-2 text-sm">
              <p class="font-medium">
                {{ formatTranslationVersion(selectedVersion) }}
              </p>
              <p class="text-muted-foreground">
                {{ formatStaff(selectedVersion.staff) }}
              </p>
              <RouterLink
                class="inline-flex items-center gap-2 text-primary hover:underline"
                :to="`/translations/${selectedVersion.id}`"
              >
                <FileText :size="16" />
                版本详情链接
              </RouterLink>
            </CardContent>
          </Card>
        </aside>

        <section class="space-y-3">
          <div class="flex items-center justify-between gap-3">
            <h2 class="text-base font-semibold">
              原文与译文对照
            </h2>
            <p class="text-sm text-muted-foreground">
              {{ readerLines.length }} 行
            </p>
          </div>

          <ErrorState v-if="translationError" :error="translationError" @retry="loadTranslationLines" />
          <LoadingState v-else-if="translationLoading" label="加载译文行" />
          <EmptyState
            v-else-if="sourceLines.length === 0"
            title="暂无原文行"
            description="该剧情尚未同步原文行。"
          />

          <div v-else class="space-y-2">
            <article
              v-for="line in readerLines"
              :id="`line-${line.source.lineNo}`"
              :key="line.source.id"
              class="rounded-md border p-4 transition"
              :class="[
                getLineClass(line.source.lineType),
                highlightedLine === line.source.lineNo ? 'ring-2 ring-primary ring-offset-2' : '',
              ]"
            >
              <div class="mb-3 flex flex-wrap items-center gap-2 text-xs text-muted-foreground">
                <span class="rounded-md bg-background px-2 py-1">#{{ line.source.lineNo }}</span>
                <span>{{ formatLineType(line.source.lineType) }}</span>
                <span v-if="line.source.speaker">原文说话人：{{ line.source.speaker }}</span>
                <span v-if="line.translation?.speaker">译文说话人：{{ line.translation.speaker }}</span>
              </div>
              <div class="grid gap-4 lg:grid-cols-2">
                <div class="rounded-md bg-background/80 p-3">
                  <p class="mb-2 text-xs font-medium text-muted-foreground">
                    原文
                  </p>
                  <p class="whitespace-pre-wrap text-sm leading-7">
                    {{ line.source.text }}
                  </p>
                </div>
                <div class="rounded-md bg-background/80 p-3">
                  <p class="mb-2 text-xs font-medium text-muted-foreground">
                    译文
                  </p>
                  <p v-if="line.translation" class="whitespace-pre-wrap text-sm leading-7">
                    {{ line.translation.text }}
                  </p>
                  <p v-else class="text-sm text-muted-foreground">
                    当前版本没有这一行译文。
                  </p>
                </div>
              </div>
            </article>
          </div>
        </section>
      </section>
    </template>
  </div>
</template>
