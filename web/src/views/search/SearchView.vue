<script setup lang="ts">
import type { StoryTypeInfo } from '@/api/assets'
import type { SearchHit } from '@/api/search'
import { BookOpen, Languages, Search } from 'lucide-vue-next'
import { computed, onMounted, ref, watch } from 'vue'
import { RouterLink, useRoute, useRouter } from 'vue-router'
import { getStoryTypes } from '@/api/assets'
import { ApiError } from '@/api/client'
import { searchAssets } from '@/api/search'
import EmptyState from '@/components/EmptyState.vue'
import ErrorState from '@/components/ErrorState.vue'
import LoadingState from '@/components/LoadingState.vue'
import PaginationControls from '@/components/PaginationControls.vue'
import SafeHighlight from '@/components/SafeHighlight.vue'
import Button from '@/components/ui/Button.vue'
import Card from '@/components/ui/Card.vue'
import CardContent from '@/components/ui/CardContent.vue'
import CardHeader from '@/components/ui/CardHeader.vue'
import CardTitle from '@/components/ui/CardTitle.vue'
import { createStoryTypeLabeler, formatStaff, formatTranslationVersion } from '@/lib/display'
import { defaultPageSize, readQueryNumber, readQueryString } from '@/lib/query'

const route = useRoute()
const router = useRouter()
const keyword = ref('')
const storyTypes = ref<StoryTypeInfo[]>([])
const results = ref<SearchHit[]>([])
const page = ref({ page: 1, pageSize: defaultPageSize, total: 0 })
const loading = ref(false)
const error = ref<ApiError | null>(null)
const recommendedKeywords = ['芸術祭', '監督', '昇降口', '初売り']
const hasSearched = computed(() => !!readQueryString(route.query.keyword))
const storyTypeLabel = computed(() => createStoryTypeLabeler(storyTypes.value))

onMounted(loadPage)
watch(() => route.fullPath, loadPage)

async function loadPage() {
  keyword.value = readQueryString(route.query.keyword)
  error.value = null
  results.value = []
  page.value = {
    page: readQueryNumber(route.query.page, 1),
    pageSize: readQueryNumber(route.query.page_size, defaultPageSize),
    total: 0,
  }

  if (!keyword.value) {
    return
  }

  loading.value = true
  try {
    const [types, response] = await Promise.all([
      storyTypes.value.length ? Promise.resolve(storyTypes.value) : getStoryTypes(),
      searchAssets({
        keyword: keyword.value,
        page: page.value.page,
        pageSize: page.value.pageSize,
      }),
    ])
    storyTypes.value = types
    results.value = response.items
    page.value = response.page
  }
  catch (loadError) {
    error.value = loadError instanceof ApiError
      ? loadError
      : new ApiError(0, '搜索失败，请稍后重试。')
  }
  finally {
    loading.value = false
  }
}

function submitSearch() {
  const text = keyword.value.trim()
  router.push({
    path: '/search',
    query: text ? { keyword: text, page: '1', page_size: String(defaultPageSize) } : {},
  })
}

function changePage(nextPage: number) {
  router.push({
    path: '/search',
    query: {
      keyword: keyword.value,
      page: String(nextPage),
      page_size: String(page.value.pageSize),
    },
  })
}

function searchRecommendedKeyword(text: string) {
  keyword.value = text
  router.push({
    path: '/search',
    query: { keyword: text, page: '1', page_size: String(defaultPageSize) },
  })
}

function getResultTarget(hit: SearchHit) {
  return {
    path: `/stories/${hit.storyId}`,
    query: {
      line: String(hit.lineNo),
      ...(hit.translationVersionId ? { translation_version_id: String(hit.translationVersionId) } : {}),
    },
  }
}

function getAssetTypeLabel(hit: SearchHit) {
  return hit.assetType === 'translation' ? '译文命中' : '原文命中'
}

function getVersionLabel(hit: SearchHit) {
  const translation = hit.translations.find(item => item.translationVersionId === hit.translationVersionId)
  return translation
    ? formatTranslationVersion({
        id: translation.translationVersionId,
        storyId: hit.storyId,
        versionNo: translation.versionNo,
        title: translation.translationVersionTitle,
        metadata: null,
        staff: translation.staff,
        createdBy: 0,
        createdAt: '',
        updatedAt: '',
      })
    : ''
}
</script>

<template>
  <div class="space-y-6">
    <section class="rounded-lg border bg-card p-6">
      <p class="text-sm text-muted-foreground">
        统一搜索
      </p>
      <div class="mt-4 space-y-5">
        <div>
          <h1 class="text-2xl font-semibold tracking-normal">
            搜索原文和当前租户译文
          </h1>
          <p class="mt-3 max-w-2xl text-sm leading-6 text-muted-foreground">
            如果不确定怎么翻译的话，不妨搜索一下之前的翻译吧。
          </p>
        </div>
        <div class="max-w-3xl space-y-3">
          <form class="flex flex-col gap-2 sm:flex-row" @submit.prevent="submitSearch">
            <div class="relative min-w-0 flex-1">
              <Search :size="18" class="absolute left-3 top-1/2 -translate-y-1/2 text-muted-foreground" />
              <input
                v-model="keyword"
                class="w-full rounded-md border bg-background py-2 pl-10 pr-3 text-sm outline-none focus:ring-2 focus:ring-ring"
                placeholder="输入关键词"
                type="search"
              >
            </div>
            <Button>搜索</Button>
          </form>
          <div class="flex flex-wrap items-center gap-2">
            <span class="text-xs text-muted-foreground">随便试试</span>
            <Button
              v-for="item in recommendedKeywords"
              :key="item"
              type="button"
              variant="outline"
              size="sm"
              class="h-8"
              @click="searchRecommendedKeyword(item)"
            >
              {{ item }}
            </Button>
          </div>
        </div>
      </div>
    </section>

    <EmptyState
      v-if="!hasSearched"
      title="输入关键词开始搜索"
      description="如果不确定怎么翻译的话，不妨搜索一下之前的翻译吧。"
    />
    <LoadingState v-else-if="loading" label="搜索中" />
    <ErrorState v-else-if="error" :error="error" @retry="loadPage" />
    <EmptyState
      v-else-if="results.length === 0"
      title="没有匹配结果"
      description="调整关键词后再试。"
    />

    <section v-else class="space-y-4">
      <div class="flex items-center justify-between gap-3">
        <p class="text-sm text-muted-foreground">
          共 {{ page.total }} 条结果
        </p>
      </div>

      <Card v-for="hit in results" :key="`${hit.assetType}-${hit.sourceLineId}-${hit.translationLineId || 0}`">
        <CardHeader>
          <div class="flex flex-wrap items-center gap-2 text-xs text-muted-foreground">
            <span class="rounded-md bg-primary/10 px-2 py-1 text-primary">{{ getAssetTypeLabel(hit) }}</span>
            <span>{{ storyTypeLabel(hit.storyType) }}</span>
            <span>/</span>
            <span>{{ hit.storyGroupTitle || '未归属剧情集' }}</span>
            <span>/</span>
            <span>第 {{ hit.lineNo }} 行</span>
          </div>
          <CardTitle class="text-base">
            {{ hit.storyTitle }}
          </CardTitle>
        </CardHeader>
        <CardContent class="space-y-4">
          <RouterLink
            :to="getResultTarget(hit)"
            class="block rounded-md border bg-background p-4 transition hover:border-primary/60 hover:shadow-sm"
          >
            <div class="mb-2 flex items-center gap-2 text-xs text-muted-foreground">
              <BookOpen v-if="hit.assetType === 'source'" :size="16" class="text-primary" />
              <Languages v-else :size="16" class="text-primary" />
              <span v-if="hit.speaker">{{ hit.speaker }}</span>
              <span v-if="hit.assetType === 'translation' && getVersionLabel(hit)">
                {{ getVersionLabel(hit) }}
              </span>
            </div>
            <p class="whitespace-pre-wrap text-sm leading-7">
              <SafeHighlight :text="hit.highlightText" />
            </p>
          </RouterLink>

          <div class="grid gap-3 lg:grid-cols-2">
            <div class="rounded-md border bg-background p-3">
              <p class="mb-2 text-xs font-medium text-muted-foreground">
                原文上下文
              </p>
              <p v-if="hit.source" class="whitespace-pre-wrap text-sm leading-6">
                <span v-if="hit.source.speaker" class="text-muted-foreground">{{ hit.source.speaker }}：</span>{{ hit.source.text }}
              </p>
              <p v-else class="text-sm text-muted-foreground">
                暂无原文上下文。
              </p>
            </div>
            <div class="rounded-md border bg-background p-3">
              <p class="mb-2 text-xs font-medium text-muted-foreground">
                当前租户译文
              </p>
              <div v-if="hit.translations.length" class="space-y-3">
                <div v-for="translation in hit.translations.slice(0, 2)" :key="translation.translationLineId">
                  <p class="text-xs text-muted-foreground">
                    第 {{ translation.versionNo }} 版 · {{ translation.translationVersionTitle || '未命名译文' }}
                  </p>
                  <p class="mt-1 whitespace-pre-wrap text-sm leading-6">
                    <span v-if="translation.speaker" class="text-muted-foreground">{{ translation.speaker }}：</span>{{ translation.text }}
                  </p>
                  <p class="mt-1 text-xs text-muted-foreground">
                    {{ formatStaff(translation.staff) }}
                  </p>
                </div>
              </div>
              <p v-else class="text-sm text-muted-foreground">
                当前租户暂无对应译文。
              </p>
            </div>
          </div>
        </CardContent>
      </Card>

      <PaginationControls :page="page" @change="changePage" />
    </section>
  </div>
</template>
